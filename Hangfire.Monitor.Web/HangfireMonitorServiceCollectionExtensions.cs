using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Monitoring;
using Hangfire.Monitor.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Hangfire.Monitor.Web;

/// <summary>
/// Registers Hangfire Monitor orchestration collaborators for DI (HM-051).
/// </summary>
public static class HangfireMonitorServiceCollectionExtensions
{
    public static IServiceCollection AddHangfireMonitorServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ApplicationMonitoringRules>();
        services.AddSingleton<SqlServerStorageFactory>();
        services.AddSingleton<FailedJobCountReader>();
        services.AddSingleton<LastFailedAtReader>();
        services.AddSingleton<HangfireStorageReader>();
        services.AddSingleton<ConfiguredApplicationsMonitor>();

        return services;
    }
}
