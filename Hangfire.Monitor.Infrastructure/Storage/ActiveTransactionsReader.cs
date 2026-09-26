using System.Data.Common;
using Hangfire;
using Hangfire.Monitor.Domain;
using Hangfire.SqlServer;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Reads aggregate active-transaction metrics for the current Hangfire storage database
/// via <c>sys.dm_tran_active_transactions</c> and <c>sys.dm_tran_database_transactions</c>.
/// </summary>
public class ActiveTransactionsReader
{
    /// <summary>
    /// Returns active-transaction metrics for the database opened by
    /// <paramref name="storage"/>. Does not set <see cref="JobStorage.Current"/>
    /// or write to Hangfire tables.
    /// </summary>
    public ActiveTransactionMetrics GetActiveTransactions(SqlServerStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        return SqlServerStorageDb.UseConnection(storage, GetActiveTransactions);
    }

    /// <summary>
    /// Executes the active-transactions query against an already-open connection. Used by unit
    /// tests and by <see cref="GetActiveTransactions(SqlServerStorage)"/> after Hangfire opens
    /// a connection.
    /// </summary>
    internal ActiveTransactionMetrics GetActiveTransactions(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var sql = ActiveTransactionsQuery.Build();

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        return ActiveTransactionsQuery.Read(reader);
    }
}
