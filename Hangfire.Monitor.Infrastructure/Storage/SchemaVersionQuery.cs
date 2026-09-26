namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Builds the read-only SQL for Hangfire storage schema version,
/// and maps the scalar result. Separated from execution so unit tests need no SQL Server.
/// </summary>
internal static class SchemaVersionQuery
{
    /// <summary>
    /// Escapes a SQL Server schema identifier the same way Hangfire.SqlServer 1.8.25 does
    /// (<c>]</c> → <c>]]</c>) before embedding it in <c>[schema]</c> brackets.
    /// </summary>
    private static string EscapeSchemaName(string schemaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        return schemaName.Replace("]", "]]", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns <c>SELECT [Version]</c> from the Hangfire <c>Schema</c> table in
    /// <paramref name="schemaName"/>.
    /// </summary>
    public static string Build(string schemaName)
    {
        var escapedSchema = EscapeSchemaName(schemaName);

        // Schema is an identifier, not a value parameter — brackets + Hangfire-style ] escaping.
        return
            $"""
            SELECT [Version]
            FROM [{escapedSchema}].[Schema]
            """;
    }

    /// <summary>
    /// Maps <see cref="System.Data.Common.DbCommand.ExecuteScalar"/> output to <see cref="int?"/>.
    /// </summary>
    public static int? ReadScalar(object? value)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        return Convert.ToInt32(value);
    }
}
