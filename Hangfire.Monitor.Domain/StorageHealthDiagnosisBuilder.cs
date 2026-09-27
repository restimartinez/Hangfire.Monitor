using System.Globalization;
using System.Text;

namespace Hangfire.Monitor.Domain;

/// <summary>
/// Builds human-readable diagnosis text and external DBA resolution guides.
/// Never executes SQL.
/// </summary>
public static class StorageHealthDiagnosisBuilder
{
    public const string HealthyDiagnosis =
        "No storage capacity issue detected from the available metrics.";

    public const string HeadroomUnavailableDiagnosis =
        "Data file headroom metrics were not available; capacity was not evaluated from MaxSize or volume stats.";

    public const string ProductionImpactWarning =
        "Changing file size or MAXSIZE can affect production. Review growth, volume free space, and maintenance windows before applying changes. Hangfire Monitor never executes corrective SQL.";

    public const string RequiredPermissionsText =
        "Diagnostic queries typically need VIEW DATABASE STATE and, for volume stats, VIEW SERVER STATE (or VIEW SERVER PERFORMANCE STATE on SQL Server 2022+). Corrective ALTER DATABASE / MODIFY FILE requires ALTER permission on the database. Run only from an authorized DBA session (for example SSMS), never from Hangfire Monitor.";

    /// <summary>
    /// Builds diagnosis for a successful capacity evaluation.
    /// </summary>
    public static string BuildDiagnosis(
        StorageHealthStatus status,
        DataFileHeadroomHealthResult? headroom,
        string? failureReason)
    {
        if (status == StorageHealthStatus.UNAVAILABLE)
        {
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                return "Storage health metrics could not be obtained: " + failureReason.Trim();
            }

            return "Storage health metrics could not be obtained for this application.";
        }

        if (status == StorageHealthStatus.WARNING && headroom?.TriggeringFile is not null)
        {
            return headroom.Diagnosis;
        }

        if (headroom is null)
        {
            return HeadroomUnavailableDiagnosis + " " + HealthyDiagnosis;
        }

        return HealthyDiagnosis;
    }

    /// <summary>
    /// Builds a resolution guide when capacity Warning is present; otherwise null.
    /// </summary>
    public static StorageResolutionGuide? BuildResolution(
        StorageHealthStatus status,
        DataFileHeadroomHealthResult? headroom,
        string? databaseNameHint)
    {
        if (status != StorageHealthStatus.WARNING || headroom?.TriggeringFile is null)
        {
            return null;
        }

        var file = headroom.TriggeringFile;
        var dbPlaceholder = string.IsNullOrWhiteSpace(databaseNameHint)
            ? "<database_name>"
            : databaseNameHint.Trim();
        var filePlaceholder = string.IsNullOrWhiteSpace(file.FileName)
            ? "<logical_file_name>"
            : file.FileName;

        var maxSizeText = file.MaxSizeKind switch
        {
            DataFileMaxSizeKind.NoGrowth => "NO_GROWTH",
            DataFileMaxSizeKind.Limited when file.MaxSizeMB is not null =>
                file.MaxSizeMB.Value.ToString("0.###", CultureInfo.InvariantCulture) + " MB",
            DataFileMaxSizeKind.Unlimited => "UNLIMITED",
            _ => "unknown"
        };

        var whatIsHappening =
            $"The data file '{file.FileName}' has FreeMB = {file.FreeMB.ToString("0.###", CultureInfo.InvariantCulture)} " +
            $"and CurrentSizeMB = {file.CurrentSizeMB.ToString("0.###", CultureInfo.InvariantCulture)} " +
            $"with MaxSize = {maxSizeText}, so SQL Server cannot grow the file further.";

        var whatToCheck =
            "Confirm the logical file name, current size, MAXSIZE, autogrowth, and free space on the hosting volume using the diagnostic queries below.";

        var recommendedAction =
            "A DBA should decide whether to raise MAXSIZE, increase the file SIZE, free unused space, or expand the volume. Hangfire Monitor only detects and explains — it does not apply changes.";

        return new StorageResolutionGuide(
            WhatIsHappening: whatIsHappening,
            WhatToCheck: whatToCheck,
            RecommendedAction: recommendedAction,
            DiagnosticQueriesSql: BuildDiagnosticQueries(),
            CorrectiveQueriesSql: BuildCorrectiveQueries(dbPlaceholder, filePlaceholder),
            RequiredPermissions: RequiredPermissionsText,
            ProductionWarning: ProductionImpactWarning);
    }

    private static string BuildDiagnosticQueries()
    {
        return
            """
            -- Diagnostic only (read-only). Run in the Hangfire database context.
            SELECT
                DB_NAME() AS DatabaseName,
                name AS FileName,
                physical_name AS PhysicalName,
                size / 128.0 AS CurrentSizeMB,
                max_size,
                CASE
                    WHEN max_size = -1 THEN 'UNLIMITED'
                    WHEN max_size = 0 THEN 'NO_GROWTH'
                    ELSE CAST(max_size / 128.0 AS varchar(30))
                END AS MaxSize,
                growth,
                is_percent_growth
            FROM sys.database_files
            WHERE type_desc = N'ROWS';

            -- Volume capacity (requires appropriate SQL Server permissions).
            SELECT
                df.name AS FileName,
                vs.volume_mount_point,
                CAST(vs.total_bytes AS float) / 1073741824.0 AS VolumeTotalGB,
                CAST(vs.available_bytes AS float) / 1073741824.0 AS VolumeFreeGB
            FROM sys.database_files AS df
            OUTER APPLY sys.dm_os_volume_stats(DB_ID(), df.file_id) AS vs
            WHERE df.type_desc = N'ROWS';
            """;
    }

    private static string BuildCorrectiveQueries(string databaseName, string logicalFileName)
    {
        var dbBracket = LooksLikePlaceholder(databaseName)
            ? "[<database_name>]"
            : SqlIdentifierEscaper.Bracket(databaseName);
        var fileLiteral = LooksLikePlaceholder(logicalFileName)
            ? "N'<logical_file_name>'"
            : SqlIdentifierEscaper.UnicodeLiteral(logicalFileName);

        var builder = new StringBuilder();
        builder.AppendLine("-- Corrective examples for authorized DBA tools only.");
        builder.AppendLine("-- Hangfire Monitor NEVER executes these statements.");
        builder.AppendLine("-- Replace <new_size_mb> with a size chosen by the DBA.");
        builder.AppendLine();
        builder.AppendLine("-- Example: grow the data file.");
        builder.AppendLine($"ALTER DATABASE {dbBracket}");
        builder.AppendLine("MODIFY FILE");
        builder.AppendLine("(");
        builder.AppendLine($"    NAME = {fileLiteral},");
        builder.AppendLine("    SIZE = <new_size_mb>MB");
        builder.AppendLine(");");
        builder.AppendLine();
        builder.AppendLine("-- Example: raise MAXSIZE (or set UNLIMITED).");
        builder.AppendLine($"ALTER DATABASE {dbBracket}");
        builder.AppendLine("MODIFY FILE");
        builder.AppendLine("(");
        builder.AppendLine($"    NAME = {fileLiteral},");
        builder.AppendLine("    MAXSIZE = UNLIMITED");
        builder.AppendLine(");");
        return builder.ToString();
    }

    private static bool LooksLikePlaceholder(string value) =>
        value.Contains('<', StringComparison.Ordinal) || value.Contains('>', StringComparison.Ordinal);
}
