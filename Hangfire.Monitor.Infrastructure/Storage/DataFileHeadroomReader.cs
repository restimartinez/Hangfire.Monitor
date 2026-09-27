using System.Data.Common;
using Hangfire;
using Hangfire.Monitor.Domain;
using Hangfire.SqlServer;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Reads per-file ROWS data-file headroom (size, MaxSize, growth, volume) for the
/// current Hangfire storage database.
/// </summary>
public class DataFileHeadroomReader
{
    /// <summary>
    /// Returns headroom metrics for ROWS files in the database opened by
    /// <paramref name="storage"/>. Does not set <see cref="JobStorage.Current"/>
    /// or write to Hangfire tables.
    /// </summary>
    public IReadOnlyList<DataFileHeadroomFileMetrics> GetDataFileHeadroom(SqlServerStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        return SqlServerStorageDb.UseConnection(storage, GetDataFileHeadroom);
    }

    /// <summary>
    /// Executes the headroom query against an already-open connection.
    /// </summary>
    internal IReadOnlyList<DataFileHeadroomFileMetrics> GetDataFileHeadroom(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var sql = DataFileHeadroomQuery.Build();

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        return DataFileHeadroomQuery.Read(reader);
    }
}
