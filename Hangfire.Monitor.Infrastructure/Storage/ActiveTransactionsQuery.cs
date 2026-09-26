using System.Data.Common;
using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Builds the read-only SQL for database-scoped active transactions,
/// and maps the result row. Separated from execution so unit tests need no SQL Server.
/// </summary>
internal static class ActiveTransactionsQuery
{
    /// <summary>
    /// Returns an aggregate <c>SELECT</c> over active transactions for the current database
    /// (<c>database_id = DB_ID()</c>, <c>transaction_state = 2</c>).
    /// </summary>
    public static string Build()
    {
        return
            """
            SELECT
                COUNT(*) AS ActiveTransactionCount,
                MIN(at.transaction_begin_time) AS OldestBeginTime,
                MAX(DATEDIFF(SECOND, at.transaction_begin_time, SYSDATETIME())) AS OldestDurationSeconds
            FROM sys.dm_tran_active_transactions AS at
            INNER JOIN sys.dm_tran_database_transactions AS dt
                ON dt.transaction_id = at.transaction_id
            WHERE at.transaction_state = 2
              AND dt.database_id = DB_ID()
            """;
    }

    /// <summary>
    /// Maps a single result row to <see cref="ActiveTransactionMetrics"/>.
    /// Expects columns in order: ActiveTransactionCount, OldestBeginTime, OldestDurationSeconds.
    /// </summary>
    public static ActiveTransactionMetrics Read(DbDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (!reader.Read())
        {
            throw new InvalidOperationException(
                "Active transactions query returned no rows.");
        }

        return MapRow(reader.GetValue(0), reader.GetValue(1), reader.GetValue(2));
    }

    /// <summary>
    /// Maps column values to <see cref="ActiveTransactionMetrics"/>.
    /// When <c>Count</c> is 0, oldest begin/duration must be null.
    /// When <c>Count</c> is greater than 0, oldest begin/duration are required.
    /// </summary>
    public static ActiveTransactionMetrics MapRow(
        object? count,
        object? oldestBeginTime,
        object? oldestDurationSeconds)
    {
        var transactionCount = RequireInt32(count, nameof(ActiveTransactionMetrics.Count));
        if (transactionCount < 0)
        {
            throw new InvalidOperationException(
                "Active transaction count cannot be negative.");
        }

        if (transactionCount == 0)
        {
            if (!IsNull(oldestBeginTime) || !IsNull(oldestDurationSeconds))
            {
                throw new InvalidOperationException(
                    "Oldest transaction fields must be null when active transaction count is zero.");
            }

            return new ActiveTransactionMetrics(
                Count: 0,
                OldestBeginTimeUtc: null,
                OldestDurationSeconds: null);
        }

        return new ActiveTransactionMetrics(
            transactionCount,
            RequireUtcDateTime(oldestBeginTime, nameof(ActiveTransactionMetrics.OldestBeginTimeUtc)),
            RequireInt32(oldestDurationSeconds, nameof(ActiveTransactionMetrics.OldestDurationSeconds)));
    }

    private static bool IsNull(object? value) => value is null || value is DBNull;

    private static int RequireInt32(object? value, string fieldName)
    {
        if (IsNull(value))
        {
            throw new InvalidOperationException(
                $"Active transaction metric '{fieldName}' was null.");
        }

        return Convert.ToInt32(value);
    }

    /// <summary>
    /// Maps SQL <c>datetime</c> to <see cref="DateTime"/> with
    /// <see cref="DateTimeKind.Utc"/> (same approach as <see cref="LastFailedAtQuery.ReadScalar"/>).
    /// </summary>
    private static DateTime RequireUtcDateTime(object? value, string fieldName)
    {
        if (IsNull(value))
        {
            throw new InvalidOperationException(
                $"Active transaction metric '{fieldName}' was null.");
        }

        var dateTime = Convert.ToDateTime(value);
        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }
}
