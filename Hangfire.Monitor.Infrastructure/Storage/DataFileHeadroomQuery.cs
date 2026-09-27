using System.Data.Common;
using System.Globalization;
using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Builds the read-only SQL for per-file data-file headroom (ROWS) and maps result rows.
/// Separated from execution so unit tests need no SQL Server.
/// </summary>
internal static class DataFileHeadroomQuery
{
    /// <summary>
    /// Returns a SELECT of ROWS files with FILEPROPERTY SpaceUsed and optional volume stats.
    /// </summary>
    public static string Build()
    {
        return
            """
            SELECT
                df.file_id AS FileId,
                df.name AS FileName,
                CAST(df.size AS bigint) / 128.0 AS CurrentSizeMB,
                CAST(FILEPROPERTY(df.name, N'SpaceUsed') AS bigint) / 128.0 AS UsedMB,
                (CAST(df.size AS bigint)
                    - CAST(FILEPROPERTY(df.name, N'SpaceUsed') AS bigint)) / 128.0 AS FreeMB,
                CASE
                    WHEN df.size > 0
                    THEN 100.0 * CAST(FILEPROPERTY(df.name, N'SpaceUsed') AS bigint)
                               / CAST(df.size AS bigint)
                    ELSE NULL
                END AS FileUsedPercent,
                df.max_size AS MaxSizePages,
                CASE
                    WHEN df.is_percent_growth = 1 THEN NULL
                    ELSE CAST(df.growth AS bigint) / 128.0
                END AS GrowthMB,
                CASE
                    WHEN df.is_percent_growth = 1 THEN df.growth
                    ELSE NULL
                END AS GrowthPercent,
                df.is_percent_growth AS IsPercentGrowth,
                vs.volume_mount_point AS VolumeMountPoint,
                CAST(vs.total_bytes AS float) / 1073741824.0 AS VolumeTotalGB,
                CAST(vs.available_bytes AS float) / 1073741824.0 AS VolumeFreeGB,
                CASE
                    WHEN vs.total_bytes IS NULL OR vs.total_bytes = 0 THEN NULL
                    ELSE 100.0 * CAST(vs.available_bytes AS float)
                               / CAST(vs.total_bytes AS float)
                END AS VolumeFreePercent
            FROM sys.database_files AS df
            OUTER APPLY sys.dm_os_volume_stats(DB_ID(), df.file_id) AS vs
            WHERE df.type_desc = N'ROWS'
            ORDER BY df.file_id
            """;
    }

    /// <summary>
    /// Reads all ROWS file headroom rows from <paramref name="reader"/>.
    /// </summary>
    public static IReadOnlyList<DataFileHeadroomFileMetrics> Read(DbDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var files = new List<DataFileHeadroomFileMetrics>();
        while (reader.Read())
        {
            files.Add(MapRow(
                reader.GetValue(0),
                reader.GetValue(1),
                reader.GetValue(2),
                reader.GetValue(3),
                reader.GetValue(4),
                reader.IsDBNull(5) ? null : reader.GetValue(5),
                reader.GetValue(6),
                reader.IsDBNull(7) ? null : reader.GetValue(7),
                reader.IsDBNull(8) ? null : reader.GetValue(8),
                reader.GetValue(9),
                reader.IsDBNull(10) ? null : reader.GetValue(10),
                reader.IsDBNull(11) ? null : reader.GetValue(11),
                reader.IsDBNull(12) ? null : reader.GetValue(12),
                reader.IsDBNull(13) ? null : reader.GetValue(13)));
        }

        return files;
    }

    /// <summary>
    /// Maps column values to <see cref="DataFileHeadroomFileMetrics"/>.
    /// </summary>
    public static DataFileHeadroomFileMetrics MapRow(
        object? fileId,
        object? fileName,
        object? currentSizeMb,
        object? usedMb,
        object? freeMb,
        object? usedPercent,
        object? maxSizePages,
        object? growthMb,
        object? growthPercent,
        object? isPercentGrowth,
        object? volumeMountPoint,
        object? volumeTotalGb,
        object? volumeFreeGb,
        object? volumeFreePercent)
    {
        var maxSizePagesValue = RequireInt64(maxSizePages, "MaxSizePages");
        var (maxSizeKind, maxSizeMb) = InterpretMaxSize(maxSizePagesValue);

        return new DataFileHeadroomFileMetrics(
            FileId: RequireInt32(fileId, nameof(DataFileHeadroomFileMetrics.FileId)),
            FileName: RequireString(fileName, nameof(DataFileHeadroomFileMetrics.FileName)),
            CurrentSizeMB: RequireDecimal(currentSizeMb, nameof(DataFileHeadroomFileMetrics.CurrentSizeMB)),
            UsedMB: RequireDecimal(usedMb, nameof(DataFileHeadroomFileMetrics.UsedMB)),
            FreeMB: RequireDecimal(freeMb, nameof(DataFileHeadroomFileMetrics.FreeMB)),
            UsedPercent: OptionalDecimal(usedPercent),
            MaxSizeKind: maxSizeKind,
            MaxSizeMB: maxSizeMb,
            GrowthMB: OptionalDecimal(growthMb),
            GrowthPercent: OptionalInt32(growthPercent),
            IsPercentGrowth: RequireBool(isPercentGrowth, nameof(DataFileHeadroomFileMetrics.IsPercentGrowth)),
            VolumeMountPoint: OptionalString(volumeMountPoint),
            VolumeTotalGB: OptionalDecimal(volumeTotalGb),
            VolumeFreeGB: OptionalDecimal(volumeFreeGb),
            VolumeFreePercent: OptionalDecimal(volumeFreePercent));
    }

    /// <summary>
    /// Interprets SQL Server <c>max_size</c> pages into kind + optional MB.
    /// Never invents a numeric MB for UNLIMITED.
    /// </summary>
    public static (DataFileMaxSizeKind Kind, decimal? MaxSizeMB) InterpretMaxSize(long maxSizePages)
    {
        if (maxSizePages < 0)
        {
            return (DataFileMaxSizeKind.Unlimited, null);
        }

        if (maxSizePages == 0)
        {
            return (DataFileMaxSizeKind.NoGrowth, null);
        }

        // SQL Server also documents 268435456 pages as an unlimited sentinel on some versions.
        if (maxSizePages == 268435456L)
        {
            return (DataFileMaxSizeKind.Unlimited, null);
        }

        var maxSizeMb = Math.Round(
            maxSizePages / 128.0m,
            2,
            MidpointRounding.AwayFromZero);
        return (DataFileMaxSizeKind.Limited, maxSizeMb);
    }

    private static int RequireInt32(object? value, string fieldName)
    {
        if (value is null || value is DBNull)
        {
            throw new InvalidOperationException($"Data file headroom metric '{fieldName}' was null.");
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static long RequireInt64(object? value, string fieldName)
    {
        if (value is null || value is DBNull)
        {
            throw new InvalidOperationException($"Data file headroom metric '{fieldName}' was null.");
        }

        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static string RequireString(object? value, string fieldName)
    {
        if (value is null || value is DBNull)
        {
            throw new InvalidOperationException($"Data file headroom metric '{fieldName}' was null.");
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"Data file headroom metric '{fieldName}' was empty.");
        }

        return text;
    }

    private static decimal RequireDecimal(object? value, string fieldName)
    {
        if (value is null || value is DBNull)
        {
            throw new InvalidOperationException($"Data file headroom metric '{fieldName}' was null.");
        }

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static bool RequireBool(object? value, string fieldName)
    {
        if (value is null || value is DBNull)
        {
            throw new InvalidOperationException($"Data file headroom metric '{fieldName}' was null.");
        }

        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private static decimal? OptionalDecimal(object? value)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static int? OptionalInt32(object? value)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static string? OptionalString(object? value)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
