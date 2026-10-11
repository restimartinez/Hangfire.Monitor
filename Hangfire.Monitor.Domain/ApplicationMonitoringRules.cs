namespace Hangfire.Monitor.Domain;

/// <summary>
/// Pure business rules that map failure counts into <see cref="ApplicationMonitoringResult"/>.
/// Does not catch storage exceptions; callers that need <see cref="MonitoringStatus.Unavailable"/>
/// must invoke <see cref="Unavailable"/> explicitly (exception mapping is a later task).
/// </summary>
public class ApplicationMonitoringRules
{
    /// <summary>
    /// Maps a successful failure read into <see cref="MonitoringStatus.Ok"/> or
    /// <see cref="MonitoringStatus.Failed"/>.
    /// </summary>
    /// <remarks>
    /// Rules:
    /// <list type="bullet">
    /// <item><c>FailedCount == 0</c> → <see cref="MonitoringStatus.Ok"/>, <c>LastFailedAt = null</c></item>
    /// <item><c>FailedCount &gt; 0</c> → <see cref="MonitoringStatus.Failed"/>, keep received <c>LastFailedAt</c></item>
    /// <item><c>ServerCount</c> is propagated and does not affect <see cref="MonitoringStatus"/></item>
    /// </list>
    /// </remarks>
    public ApplicationMonitoringResult FromFailureInfo(
        string applicationName,
        long failedCount,
        DateTime? lastFailedAt,
        long serverCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        if (failedCount == 0)
        {
            return new ApplicationMonitoringResult(
                applicationName,
                MonitoringStatus.Ok,
                FailedCount: 0,
                LastFailedAt: null,
                ServerCount: serverCount);
        }

        if (failedCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failedCount),
                failedCount,
                "Failed count cannot be negative.");
        }

        return new ApplicationMonitoringResult(
            applicationName,
            MonitoringStatus.Failed,
            failedCount,
            lastFailedAt,
            serverCount);
    }

    /// <summary>
    /// Builds an <see cref="MonitoringStatus.Unavailable"/> result.
    /// Does not interpret exceptions; use when the orchestration layer has already decided
    /// the application could not be monitored.
    /// </summary>
    public ApplicationMonitoringResult Unavailable(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        return new ApplicationMonitoringResult(
            applicationName,
            MonitoringStatus.Unavailable,
            FailedCount: 0,
            LastFailedAt: null,
            ServerCount: 0);
    }
}
