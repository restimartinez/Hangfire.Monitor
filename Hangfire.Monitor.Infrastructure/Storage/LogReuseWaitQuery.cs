using System.Data.Common;
using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Builds the read-only SQL for database log reuse wait metadata,
/// and maps the result row. Separated from execution so unit tests need no SQL Server.
/// </summary>
internal static class LogReuseWaitQuery
{
    /// <summary>
    /// Returns a <c>SELECT</c> over <c>sys.databases</c> for the current database
    /// (<c>database_id = DB_ID()</c>).
    /// </summary>
    public static string Build()
    {
        return
            """
            SELECT
                log_reuse_wait,
                log_reuse_wait_desc,
                recovery_model_desc
            FROM sys.databases
            WHERE database_id = DB_ID()
            """;
    }

    /// <summary>
    /// Maps a single result row to <see cref="LogReuseWaitMetrics"/>.
    /// Expects columns in order: log_reuse_wait, log_reuse_wait_desc, recovery_model_desc.
    /// </summary>
    public static LogReuseWaitMetrics Read(DbDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (!reader.Read())
        {
            throw new InvalidOperationException(
                "sys.databases returned no rows for the current database.");
        }

        return MapRow(reader.GetValue(0), reader.GetValue(1), reader.GetValue(2));
    }

    /// <summary>
    /// Maps column values to <see cref="LogReuseWaitMetrics"/>.
    /// Does not coerce <see langword="null"/> / <see cref="DBNull"/> to defaults.
    /// </summary>
    public static LogReuseWaitMetrics MapRow(
        object? wait,
        object? waitDescription,
        object? recoveryModel)
    {
        return new LogReuseWaitMetrics(
            RequireInt32(wait, nameof(LogReuseWaitMetrics.Wait)),
            RequireString(waitDescription, nameof(LogReuseWaitMetrics.WaitDescription)),
            RequireString(recoveryModel, nameof(LogReuseWaitMetrics.RecoveryModel)));
    }

    private static int RequireInt32(object? value, string fieldName)
    {
        if (value is null || value is DBNull)
        {
            throw new InvalidOperationException(
                $"Log reuse wait metric '{fieldName}' was null.");
        }

        return Convert.ToInt32(value);
    }

    private static string RequireString(object? value, string fieldName)
    {
        if (value is null || value is DBNull)
        {
            throw new InvalidOperationException(
                $"Log reuse wait metric '{fieldName}' was null.");
        }

        return Convert.ToString(value)
            ?? throw new InvalidOperationException(
                $"Log reuse wait metric '{fieldName}' was null.");
    }
}
