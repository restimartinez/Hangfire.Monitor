using System.Data.Common;
using Hangfire;
using Hangfire.Monitor.Domain;
using Hangfire.SqlServer;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Reads aggregate transaction-log space usage (Total / Used / Free MB and Used %) for the
/// current Hangfire storage database via <c>sys.dm_db_log_space_usage</c>.
/// </summary>
public class LogSpaceReader
{
    /// <summary>
    /// Returns transaction-log space metrics for the database opened by
    /// <paramref name="storage"/>. Does not set <see cref="JobStorage.Current"/>
    /// or write to Hangfire tables.
    /// </summary>
    public LogSpaceMetrics GetLogSpace(SqlServerStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        return SqlServerStorageDb.UseConnection(storage, GetLogSpace);
    }

    /// <summary>
    /// Executes the log-space query against an already-open connection. Used by unit tests and by
    /// <see cref="GetLogSpace(SqlServerStorage)"/> after Hangfire opens a connection.
    /// </summary>
    internal LogSpaceMetrics GetLogSpace(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var sql = LogSpaceQuery.Build();

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        return LogSpaceQuery.Read(reader);
    }
}
