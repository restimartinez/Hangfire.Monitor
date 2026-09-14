using System.Data.Common;
using Hangfire;
using Hangfire.SqlServer;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Reads the timestamp of the most recent job currently in the Failed state via a narrow
/// read-only SQL aggregate (<c>MAX(State.CreatedAt)</c>).
/// </summary>
public class LastFailedAtReader
{
    /// <summary>
    /// Returns the latest failure timestamp for jobs currently in Failed on
    /// <paramref name="storage"/>, or <see langword="null"/> when none exist.
    /// Does not set <see cref="JobStorage.Current"/> or write to Hangfire tables.
    /// </summary>
    public DateTime? GetLastFailedAt(SqlServerStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var schemaName = SqlServerStorageDb.GetSchemaName(storage);
        return GetLastFailedAt(storage, schemaName);
    }

    /// <summary>
    /// Executes the aggregate against an already-open connection. Used by unit tests and by
    /// <see cref="GetLastFailedAt(SqlServerStorage)"/> after Hangfire opens a connection.
    /// </summary>
    internal DateTime? GetLastFailedAt(DbConnection connection, string schemaName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        var sql = LastFailedAtQuery.Build(schemaName);

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        return LastFailedAtQuery.ReadScalar(command.ExecuteScalar());
    }

    private DateTime? GetLastFailedAt(SqlServerStorage storage, string schemaName)
    {
        return SqlServerStorageDb.UseConnection(
            storage,
            connection => GetLastFailedAt(connection, schemaName));
    }
}
