using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Monitoring;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Hangfire.Monitor.Web.Pages.Jobs;

public class FailedModel : PageModel
{
    private readonly ConfiguredApplicationsMonitor _monitor;
    private readonly IOptions<HangfireMonitorOptions> _options;

    public IReadOnlyList<ApplicationMonitoringResult> Results { get; private set; }
        = Array.Empty<ApplicationMonitoringResult>();

    /// <summary>
    /// Configured latest Hangfire package version for Version-column badges.
    /// </summary>
    public string LatestHangfireVersion { get; private set; } = string.Empty;

    public FailedModel(
        ConfiguredApplicationsMonitor monitor,
        IOptions<HangfireMonitorOptions> options)
    {
        _monitor = monitor;
        _options = options;
    }

    public void OnGet()
    {
        LatestHangfireVersion = _options.Value.LatestHangfireVersion;
        Results = _monitor.MonitorAll(
            _options.Value.Applications);
    }
}
