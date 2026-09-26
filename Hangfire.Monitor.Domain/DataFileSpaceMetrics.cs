namespace Hangfire.Monitor.Domain;

/// <summary>
/// Aggregate data-file space metrics for the current Hangfire storage database (MB).
/// Acquisition model only — no health status or thresholds.
/// </summary>
public sealed record DataFileSpaceMetrics(
    decimal AllocatedMB,
    decimal UsedMB,
    decimal FreeMB);
