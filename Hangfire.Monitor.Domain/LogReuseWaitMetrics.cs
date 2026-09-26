namespace Hangfire.Monitor.Domain;

/// <summary>
/// Log reuse wait metadata for the current Hangfire storage database.
/// Acquisition model only — no health status or interpretation of wait codes.
/// </summary>
public sealed record LogReuseWaitMetrics(
    int Wait,
    string WaitDescription,
    string RecoveryModel);
