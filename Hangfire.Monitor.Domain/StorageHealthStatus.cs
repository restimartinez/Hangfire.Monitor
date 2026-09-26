namespace Hangfire.Monitor.Domain;

/// <summary>
/// Health status for Hangfire SQL Server storage metrics.
/// Distinct from <see cref="MonitoringStatus"/> (job/application monitoring).
/// </summary>
public enum StorageHealthStatus
{
    OK,
    WARNING,
    CRITICAL,
    UNAVAILABLE
}
