namespace Hangfire.Monitor.Domain;

/// <summary>
/// Domain outcome of evaluating Hangfire storage data-file space health.
/// Construct via <see cref="DataFileSpaceHealthRules"/>.
/// </summary>
public sealed record DataFileSpaceHealthResult(
    decimal AllocatedMB,
    decimal UsedMB,
    decimal FreeMB,
    decimal? UsedPercent,
    StorageHealthStatus Status,
    string Diagnosis,
    string Recommendation);
