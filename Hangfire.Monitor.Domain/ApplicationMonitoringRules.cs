namespace Hangfire.Monitor.Domain;

/// <summary>
/// Pure business rules that map failure counts into <see cref="ApplicationMonitoringResult"/>.
/// Does not catch storage exceptions; callers that need <see cref="MonitoringStatus.UNAVAILABLE"/>
/// must invoke <see cref="Unavailable"/> explicitly (exception mapping is a later task).
/// </summary>
public class ApplicationMonitoringRules
{
    /// <summary>
    /// Maps a successful failure read into <see cref="MonitoringStatus.OK"/> or
    /// <see cref="MonitoringStatus.FAILED"/>.
    /// </summary>
    /// <remarks>
    /// Rules:
    /// <list type="bullet">
    /// <item><c>FailedCount == 0</c> → <see cref="MonitoringStatus.OK"/>, <c>LastFailedAt = null</c></item>
    /// <item><c>FailedCount &gt; 0</c> → <see cref="MonitoringStatus.FAILED"/>, keep received <c>LastFailedAt</c></item>
    /// </list>
    /// </remarks>
    public ApplicationMonitoringResult FromFailureInfo(
        string applicationName,
        long failedCount,
        DateTime? lastFailedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        if (failedCount == 0)
        {
            return new ApplicationMonitoringResult(
                applicationName,
                MonitoringStatus.OK,
                FailedCount: 0,
                LastFailedAt: null);
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
            MonitoringStatus.FAILED,
            failedCount,
            lastFailedAt);
    }

    /// <summary>
    /// Builds an <see cref="MonitoringStatus.UNAVAILABLE"/> result.
    /// Does not interpret exceptions; use when the orchestration layer has already decided
    /// the application could not be monitored.
    /// </summary>
    public ApplicationMonitoringResult Unavailable(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        return new ApplicationMonitoringResult(
            applicationName,
            MonitoringStatus.UNAVAILABLE,
            FailedCount: 0,
            LastFailedAt: null);
    }
}
