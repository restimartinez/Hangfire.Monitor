using System.Data.Common;
using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Builds the read-only SQL for database data-file space usage,
/// and maps the result row. Separated from execution so unit tests need no SQL Server.
/// </summary>
internal static class DataFileSpaceQuery
{
    /// <summary>
    /// Returns an aggregate <c>SELECT</c> over <c>sys.dm_db_file_space_usage</c>
    /// (database-scoped data file pages → MB).
    /// </summary>
    public static string Build()
    {
        return
            """
            SELECT
                SUM(total_page_count) / 128.0 AS AllocatedMB,
                SUM(allocated_extent_page_count) / 128.0 AS UsedMB,
                SUM(unallocated_extent_page_count) / 128.0 AS FreeMB
            FROM sys.dm_db_file_space_usage
            """;
    }

    /// <summary>
    /// Maps a single result row to <see cref="DataFileSpaceMetrics"/>.
    /// Expects columns in order: AllocatedMB, UsedMB, FreeMB.
    /// </summary>
    public static DataFileSpaceMetrics Read(DbDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (!reader.Read())
        {
            throw new InvalidOperationException(
                "sys.dm_db_file_space_usage returned no rows.");
        }

        return MapRow(reader.GetValue(0), reader.GetValue(1), reader.GetValue(2));
    }

    /// <summary>
    /// Maps column values to <see cref="DataFileSpaceMetrics"/>.
    /// Does not coerce <see langword="null"/> / <see cref="DBNull"/> to zero.
    /// </summary>
    public static DataFileSpaceMetrics MapRow(object? allocatedMb, object? usedMb, object? freeMb)
    {
        return new DataFileSpaceMetrics(
            RequireDecimal(allocatedMb, nameof(DataFileSpaceMetrics.AllocatedMB)),
            RequireDecimal(usedMb, nameof(DataFileSpaceMetrics.UsedMB)),
            RequireDecimal(freeMb, nameof(DataFileSpaceMetrics.FreeMB)));
    }

    private static decimal RequireDecimal(object? value, string fieldName)
    {
        if (value is null || value is DBNull)
        {
            throw new InvalidOperationException(
                $"Data file space metric '{fieldName}' was null.");
        }

        return Convert.ToDecimal(value);
    }
}
