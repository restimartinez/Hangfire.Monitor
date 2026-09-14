namespace Hangfire.Monitor.Domain;

/// <summary>
/// Domain outcome of monitoring one Hangfire application.
/// Independent of Hangfire types and Infrastructure read models.
/// Construct via <see cref="ApplicationMonitoringRules"/>.
/// </summary>
public sealed record ApplicationMonitoringResult(
    string ApplicationName,
    MonitoringStatus Status,
    long FailedCount,
    DateTime? LastFailedAt);
