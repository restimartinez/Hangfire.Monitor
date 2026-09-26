namespace Hangfire.Monitor.Domain;

/// <summary>
/// Aggregate transaction-log space metrics for the current Hangfire storage database (MB / percent).
/// Acquisition model only — no health status or thresholds.
/// </summary>
public sealed record LogSpaceMetrics(
    decimal TotalLogMB,
    decimal UsedLogMB,
    decimal FreeLogMB,
    decimal UsedPercent);
