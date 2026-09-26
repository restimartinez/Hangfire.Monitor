using System.Data.Common;
using Hangfire;
using Hangfire.Monitor.Domain;
using Hangfire.SqlServer;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Reads log reuse wait metadata (wait code, description, recovery model) for the
/// current Hangfire storage database via <c>sys.databases</c>.
/// </summary>
public class LogReuseWaitReader
{
    /// <summary>
    /// Returns log reuse wait metrics for the database opened by
    /// <paramref name="storage"/>. Does not set <see cref="JobStorage.Current"/>
    /// or write to Hangfire tables. Does not interpret wait descriptions.
    /// </summary>
    public LogReuseWaitMetrics GetLogReuseWait(SqlServerStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        return SqlServerStorageDb.UseConnection(storage, GetLogReuseWait);
    }

    /// <summary>
    /// Executes the log-reuse-wait query against an already-open connection. Used by unit tests
    /// and by <see cref="GetLogReuseWait(SqlServerStorage)"/> after Hangfire opens a connection.
    /// </summary>
    internal LogReuseWaitMetrics GetLogReuseWait(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var sql = LogReuseWaitQuery.Build();

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        return LogReuseWaitQuery.Read(reader);
    }
}
