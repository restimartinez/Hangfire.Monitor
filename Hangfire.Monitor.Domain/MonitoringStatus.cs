namespace Hangfire.Monitor.Domain;

/// <summary>
/// Functional monitoring status for a single Hangfire application.
/// </summary>
public enum MonitoringStatus
{
    Ok,
    Failed,
    Unavailable
}
