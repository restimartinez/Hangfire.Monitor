namespace Hangfire.Monitor.Domain;

/// <summary>
/// Root configuration for Hangfire Monitor (<c>HangfireMonitor</c> section).
/// </summary>
public class HangfireMonitorOptions
{
    public List<HangfireApplicationOptions> Applications { get; set; } = [];
}
