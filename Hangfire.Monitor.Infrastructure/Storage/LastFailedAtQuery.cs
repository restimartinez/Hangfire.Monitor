using Hangfire.States;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Builds the read-only aggregate SQL for the latest current Failed-state timestamp,
/// and maps the scalar result. Separated from execution so unit tests need no SQL Server.
/// </summary>
internal static class LastFailedAtQuery
{
    /// <summary>
    /// Escapes a SQL Server schema identifier the same way Hangfire.SqlServer 1.8.25 does
    /// (<c>]</c> → <c>]]</c>) before embedding it in <c>[schema]</c> brackets.
    /// </summary>
    public static string EscapeSchemaName(string schemaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        return schemaName.Replace("]", "]]", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns <c>SELECT MAX(State.CreatedAt)</c> for jobs whose current state is Failed,
    /// joined via <c>Job.StateId</c> → <c>State.Id</c> (Hangfire monitoring equivalent).
    /// </summary>
    public static string Build(string schemaName)
    {
        var escapedSchema = EscapeSchemaName(schemaName);

        // Schema is an identifier, not a value parameter — brackets + Hangfire-style ] escaping.
        // Failed state name is Hangfire's constant (not user input).
        return
            $"""
            SELECT MAX(s.[CreatedAt])
            FROM [{escapedSchema}].[Job] AS j
            INNER JOIN [{escapedSchema}].[State] AS s
                ON j.[StateId] = s.[Id] AND j.[Id] = s.[JobId]
            WHERE j.[StateName] = N'{FailedState.StateName}'
            """;
    }

    /// <summary>
    /// Maps <see cref="System.Data.Common.DbCommand.ExecuteScalar"/> output to <see cref="DateTime?"/>.
    /// Hangfire stores <c>State.CreatedAt</c> as UTC; ADO.NET typically returns
    /// <see cref="DateTimeKind.Unspecified"/>, which is normalized to <see cref="DateTimeKind.Utc"/>
    /// here. No local-time conversion is applied.
    /// </summary>
    public static DateTime? ReadScalar(object? value)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        var dateTime = Convert.ToDateTime(value);
        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }
}
