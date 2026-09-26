using System.Data.Common;
using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Builds the read-only SQL for database transaction-log space usage,
/// and maps the result row. Separated from execution so unit tests need no SQL Server.
/// </summary>
internal static class LogSpaceQuery
{
    /// <summary>
    /// Returns a <c>SELECT</c> over <c>sys.dm_db_log_space_usage</c>
    /// (database-scoped log size / used / free MB and used percent).
    /// </summary>
    public static string Build()
    {
        return
            """
            SELECT
                CAST(total_log_size_in_bytes / 1048576.0 AS decimal(18, 2)) AS TotalLogMB,
                CAST(used_log_space_in_bytes / 1048576.0 AS decimal(18, 2)) AS UsedLogMB,
                CAST(
                    (total_log_size_in_bytes - used_log_space_in_bytes) / 1048576.0
                    AS decimal(18, 2)
                ) AS FreeLogMB,
                CAST(used_log_space_in_percent AS decimal(18, 2)) AS UsedPercent
            FROM sys.dm_db_log_space_usage
            """;
    }

    /// <summary>
    /// Maps a single result row to <see cref="LogSpaceMetrics"/>.
    /// Expects columns in order: TotalLogMB, UsedLogMB, FreeLogMB, UsedPercent.
    /// </summary>
    public static LogSpaceMetrics Read(DbDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (!reader.Read())
        {
            throw new InvalidOperationException(
                "sys.dm_db_log_space_usage returned no rows.");
        }

        return MapRow(
            reader.GetValue(0),
            reader.GetValue(1),
            reader.GetValue(2),
            reader.GetValue(3));
    }

    /// <summary>
    /// Maps column values to <see cref="LogSpaceMetrics"/>.
    /// Does not coerce <see langword="null"/> / <see cref="DBNull"/> to zero.
    /// </summary>
    public static LogSpaceMetrics MapRow(
        object? totalLogMb,
        object? usedLogMb,
        object? freeLogMb,
        object? usedPercent)
    {
        return new LogSpaceMetrics(
            RequireDecimal(totalLogMb, nameof(LogSpaceMetrics.TotalLogMB)),
            RequireDecimal(usedLogMb, nameof(LogSpaceMetrics.UsedLogMB)),
            RequireDecimal(freeLogMb, nameof(LogSpaceMetrics.FreeLogMB)),
            RequireDecimal(usedPercent, nameof(LogSpaceMetrics.UsedPercent)));
    }

    private static decimal RequireDecimal(object? value, string fieldName)
    {
        if (value is null || value is DBNull)
        {
            throw new InvalidOperationException(
                $"Transaction log space metric '{fieldName}' was null.");
        }

        return Convert.ToDecimal(value);
    }
}
