using System.Data.Common;
using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Storage;

namespace Hangfire.Monitor.Infrastructure.Monitoring;

/// <summary>
/// Monitors every configured Hangfire application independently and returns one
/// <see cref="ApplicationMonitoringResult"/> per application in configuration order.
/// </summary>
public class ConfiguredApplicationsMonitor
{
    private readonly Func<HangfireApplicationOptions, HangfireApplicationFailureInfo> _readFailureInfo;
    private readonly ApplicationMonitoringRules _rules;

    public ConfiguredApplicationsMonitor(
        HangfireStorageReader storageReader,
        ApplicationMonitoringRules rules)
    {
        ArgumentNullException.ThrowIfNull(storageReader);
        ArgumentNullException.ThrowIfNull(rules);

        _readFailureInfo = storageReader.Read;
        _rules = rules;
    }

    /// <summary>
    /// Test seam: substitute the per-application storage read without a live SQL Server.
    /// </summary>
    internal ConfiguredApplicationsMonitor(
        Func<HangfireApplicationOptions, HangfireApplicationFailureInfo> readFailureInfo,
        ApplicationMonitoringRules rules)
    {
        _readFailureInfo = readFailureInfo ?? throw new ArgumentNullException(nameof(readFailureInfo));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    /// <summary>
    /// Monitors each application in <paramref name="applications"/> order.
    /// <see cref="DbException"/> for one app becomes <see cref="MonitoringStatus.UNAVAILABLE"/>
    /// for that app only; other apps continue. Non-<see cref="DbException"/> errors propagate.
    /// </summary>
    public IReadOnlyList<ApplicationMonitoringResult> MonitorAll(
        IEnumerable<HangfireApplicationOptions> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);

        var results = new List<ApplicationMonitoringResult>();

        foreach (var application in applications)
        {
            ArgumentNullException.ThrowIfNull(application);

            try
            {
                var failureInfo = _readFailureInfo(application);
                results.Add(_rules.FromFailureInfo(
                    application.Name,
                    failureInfo.FailedCount,
                    failureInfo.LastFailedAt,
                    failureInfo.ServerCount));
            }
            catch (DbException)
            {
                results.Add(_rules.Unavailable(application.Name));
            }
        }

        return results;
    }
}
