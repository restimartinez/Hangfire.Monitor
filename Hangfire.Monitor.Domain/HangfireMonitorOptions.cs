namespace Hangfire.Monitor.Domain;

/// <summary>
/// Root configuration for Hangfire Monitor (<c>HangfireMonitor</c> section).
/// </summary>
public class HangfireMonitorOptions
{
    /// <summary>
    /// Latest known Hangfire package version used to color the Version column
    /// (e.g. <c>1.8.25</c>). Optional; when unset/invalid, Version badges are unavailable.
    /// </summary>
    public string LatestHangfireVersion { get; set; } = string.Empty;

    public List<HangfireApplicationOptions> Applications { get; set; } = [];
}
