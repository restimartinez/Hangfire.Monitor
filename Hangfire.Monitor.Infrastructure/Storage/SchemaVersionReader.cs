using System.Data.Common;
using Hangfire;
using Hangfire.SqlServer;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Reads the Hangfire SQL Server storage schema version via a narrow read-only
/// <c>SELECT [Version]</c> against the Hangfire <c>Schema</c> table.
/// </summary>
public class SchemaVersionReader
{
    /// <summary>
    /// Returns the schema version stored for <paramref name="storage"/>, or
    /// <see langword="null"/> when the query yields no value.
    /// Does not set <see cref="JobStorage.Current"/> or write to Hangfire tables.
    /// </summary>
    public int? GetSchemaVersion(SqlServerStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var schemaName = SqlServerStorageDb.GetSchemaName(storage);
        return GetSchemaVersion(storage, schemaName);
    }

    /// <summary>
    /// Executes the version query against an already-open connection. Used by unit tests and by
    /// <see cref="GetSchemaVersion(SqlServerStorage)"/> after Hangfire opens a connection.
    /// </summary>
    internal int? GetSchemaVersion(DbConnection connection, string schemaName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        var sql = SchemaVersionQuery.Build(schemaName);

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        return SchemaVersionQuery.ReadScalar(command.ExecuteScalar());
    }

    private int? GetSchemaVersion(SqlServerStorage storage, string schemaName)
    {
        return SqlServerStorageDb.UseConnection(
            storage,
            connection => GetSchemaVersion(connection, schemaName));
    }
}
