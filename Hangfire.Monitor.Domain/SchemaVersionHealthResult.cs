namespace Hangfire.Monitor.Domain;

/// <summary>
/// Domain outcome of evaluating Hangfire SQL Server schema version health.
/// Construct via <see cref="SchemaVersionHealthRules"/>.
/// </summary>
public sealed record SchemaVersionHealthResult(
    int? ActualVersion,
    int ExpectedVersion,
    StorageHealthStatus Status,
    string Diagnosis,
    string Recommendation);
