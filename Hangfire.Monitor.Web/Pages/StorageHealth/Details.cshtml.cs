using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Monitoring;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Hangfire.Monitor.Web.Pages.StorageHealth;

public class DetailsModel : PageModel
{
    private readonly ConfiguredApplicationsStorageHealthMonitor _monitor;
    private readonly IOptions<HangfireMonitorOptions> _options;

    public StorageHealthDetails? Details { get; private set; }

    public DetailsModel(
        ConfiguredApplicationsStorageHealthMonitor monitor,
        IOptions<HangfireMonitorOptions> options)
    {
        _monitor = monitor;
        _options = options;
    }

    public IActionResult OnGet(string applicationName)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            return NotFound();
        }

        var application = FindConfiguredApplication(applicationName);
        if (application is null)
        {
            return NotFound();
        }

        Details = StorageHealthDetails.From(_monitor.MonitorOne(application));
        return Page();
    }

    private HangfireApplicationOptions? FindConfiguredApplication(string applicationName)
    {
        foreach (var application in _options.Value.Applications)
        {
            if (string.Equals(application.Name, applicationName, StringComparison.Ordinal))
            {
                return application;
            }
        }

        return null;
    }
}
