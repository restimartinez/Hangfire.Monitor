using System.Data.Common;
using Hangfire;
using Hangfire.Monitor.Domain;
using Hangfire.SqlServer;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Reads aggregate data-file space usage (Allocated / Used / Free MB) for the
/// current Hangfire storage database via <c>sys.dm_db_file_space_usage</c>.
/// </summary>
public class DataFileSpaceReader
{
    /// <summary>
    /// Returns data-file space metrics for the database opened by
    /// <paramref name="storage"/>. Does not set <see cref="JobStorage.Current"/>
    /// or write to Hangfire tables.
    /// </summary>
    public DataFileSpaceMetrics GetDataFileSpace(SqlServerStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        return SqlServerStorageDb.UseConnection(storage, GetDataFileSpace);
    }

    /// <summary>
    /// Executes the space query against an already-open connection. Used by unit tests and by
    /// <see cref="GetDataFileSpace(SqlServerStorage)"/> after Hangfire opens a connection.
    /// </summary>
    internal DataFileSpaceMetrics GetDataFileSpace(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var sql = DataFileSpaceQuery.Build();

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        return DataFileSpaceQuery.Read(reader);
    }
}
