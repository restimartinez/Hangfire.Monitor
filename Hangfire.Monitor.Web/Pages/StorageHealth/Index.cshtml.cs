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

    public IndexModel(
        ConfiguredApplicationsStorageHealthMonitor monitor,
        IOptions<HangfireMonitorOptions> options)
    {
        _monitor = monitor;
        _options = options;
    }

    public void OnGet()
    {
        Results = _monitor.MonitorAll(
            _options.Value.Applications);
    }
}
