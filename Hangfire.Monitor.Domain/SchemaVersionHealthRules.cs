namespace Hangfire.Monitor.Domain;

/// <summary>
/// Pure rules that map a Hangfire schema version reading into
/// <see cref="SchemaVersionHealthResult"/>. Does not catch storage exceptions;
/// callers that need <see cref="StorageHealthStatus.Unavailable"/> after a
/// <c>DbException</c> must invoke <see cref="Unavailable"/> explicitly.
/// </summary>
public class SchemaVersionHealthRules
{
    /// <summary>
    /// Default expected Hangfire SQL Server schema version for Hangfire.SqlServer 1.8.x (including 1.8.25).
    /// </summary>
    public const int DefaultExpectedSchemaVersion = 9;

    /// <summary>
    /// Maps a successful schema-version read into <see cref="StorageHealthStatus.Ok"/>,
    /// <see cref="StorageHealthStatus.Warning"/>, or <see cref="StorageHealthStatus.Unavailable"/>.
    /// </summary>
    /// <remarks>
    /// Rules:
    /// <list type="bullet">
    /// <item><c>actual == null</c> → <see cref="StorageHealthStatus.Unavailable"/></item>
    /// <item><c>actual == expected</c> → <see cref="StorageHealthStatus.Ok"/></item>
    /// <item><c>actual != expected</c> → <see cref="StorageHealthStatus.Warning"/></item>
    /// </list>
    /// Schema Version never produces <see cref="StorageHealthStatus.Critical"/>.
    /// </remarks>
    public SchemaVersionHealthResult Evaluate(int? actualVersion, int expectedVersion)
    {
        if (actualVersion is null)
        {
            return Unavailable(expectedVersion);
        }

        if (actualVersion.Value == expectedVersion)
        {
            return new SchemaVersionHealthResult(
                actualVersion,
                expectedVersion,
                StorageHealthStatus.Ok,
                Diagnosis: $"Schema version {actualVersion.Value} matches the expected version.",
                Recommendation: "No action required.");
        }

        if (actualVersion.Value < expectedVersion)
        {
            return new SchemaVersionHealthResult(
                actualVersion,
                expectedVersion,
                StorageHealthStatus.Warning,
                Diagnosis:
                $"Schema version {actualVersion.Value} is lower than the expected version {expectedVersion}.",
                Recommendation:
                "Review the Hangfire SQL Server schema before upgrading or using features that require the newer schema version.");
        }

        return new SchemaVersionHealthResult(
            actualVersion,
            expectedVersion,
            StorageHealthStatus.Warning,
            Diagnosis:
            $"Schema version {actualVersion.Value} is higher than the expected version {expectedVersion}.",
            Recommendation:
            "Review the Hangfire.SqlServer package/schema compatibility before making further changes.");
    }

    /// <summary>
    /// Builds a <see cref="StorageHealthStatus.Unavailable"/> result.
    /// Does not interpret exceptions; use when the orchestration layer has already decided
    /// the schema version could not be read.
    /// </summary>
    public SchemaVersionHealthResult Unavailable(int expectedVersion)
    {
        return new SchemaVersionHealthResult(
            ActualVersion: null,
            expectedVersion,
            StorageHealthStatus.Unavailable,
            Diagnosis: "A valid Hangfire schema version could not be obtained.",
            Recommendation:
            "Verify that the Hangfire SQL Server schema is installed and the configured schema name is correct.");
    }
}
