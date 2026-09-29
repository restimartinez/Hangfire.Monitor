using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Monitoring;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Hangfire.Monitor.Web.Pages.StorageHealth;

public class IndexModel : PageModel
{
    private readonly ConfiguredApplicationsStorageHealthMonitor _monitor;
    private readonly IOptions<HangfireMonitorOptions> _options;

    public IReadOnlyList<ApplicationStorageHealthResult> Results { get; private set; }
        = Array.Empty<ApplicationStorageHealthResult>();

    /// <summary>
    /// Configured latest Hangfire package version for Version-column badges.
    /// </summary>
    public string LatestHangfireVersion { get; private set; } = string.Empty;

    public IndexModel(
        ConfiguredApplicationsStorageHealthMonitor monitor,
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
