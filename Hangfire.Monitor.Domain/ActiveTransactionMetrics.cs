namespace Hangfire.Monitor.Domain;

/// <summary>
/// Aggregate active-transaction metrics for the current Hangfire storage database.
/// Acquisition model only — no health status or thresholds.
/// </summary>
public sealed record ActiveTransactionMetrics(
    int Count,
    DateTime? OldestBeginTimeUtc,
    int? OldestDurationSeconds);
