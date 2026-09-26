using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Monitoring;
using Hangfire.Monitor.Infrastructure.Storage;
using Hangfire.Monitor.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Hangfire.Monitor.Tests;

public class HangfireMonitorDependencyInjectionTests
{
    [Fact]
    public void AddHangfireMonitorServices_ResolvesConfiguredApplicationsMonitor()
    {
        using var provider = BuildProvider();

        var monitor = provider.GetRequiredService<ConfiguredApplicationsMonitor>();

        Assert.NotNull(monitor);
    }

    [Fact]
    public void AddHangfireMonitorServices_ResolvesConfiguredApplicationsStorageHealthMonitor()
    {
        using var provider = BuildProvider();

        var monitor = provider.GetRequiredService<ConfiguredApplicationsStorageHealthMonitor>();

        Assert.NotNull(monitor);
    }

    [Fact]
    public void AddHangfireMonitorServices_ResolvesCollaborators()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<ApplicationMonitoringRules>());
        Assert.NotNull(provider.GetRequiredService<SqlServerStorageFactory>());
        Assert.NotNull(provider.GetRequiredService<FailedJobCountReader>());
        Assert.NotNull(provider.GetRequiredService<LastFailedAtReader>());
        Assert.NotNull(provider.GetRequiredService<HangfireStorageReader>());
        Assert.NotNull(provider.GetRequiredService<ConfiguredApplicationsMonitor>());
        Assert.NotNull(provider.GetRequiredService<SchemaVersionReader>());
        Assert.NotNull(provider.GetRequiredService<DataFileSpaceReader>());
        Assert.NotNull(provider.GetRequiredService<LogSpaceReader>());
        Assert.NotNull(provider.GetRequiredService<LogReuseWaitReader>());
        Assert.NotNull(provider.GetRequiredService<ActiveTransactionsReader>());
        Assert.NotNull(provider.GetRequiredService<SchemaVersionHealthRules>());
        Assert.NotNull(provider.GetRequiredService<DataFileSpaceHealthRules>());
        Assert.NotNull(provider.GetRequiredService<ApplicationStorageHealthRules>());
        Assert.NotNull(provider.GetRequiredService<ConfiguredApplicationsStorageHealthMonitor>());
    }

    [Fact]
    public void AddHangfireMonitorServices_RegistersCollaboratorsAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddHangfireMonitorServices();

        AssertSingleton<ApplicationMonitoringRules>(services);
        AssertSingleton<SqlServerStorageFactory>(services);
        AssertSingleton<FailedJobCountReader>(services);
        AssertSingleton<LastFailedAtReader>(services);
        AssertSingleton<HangfireStorageReader>(services);
        AssertSingleton<ConfiguredApplicationsMonitor>(services);
        AssertSingleton<SchemaVersionReader>(services);
        AssertSingleton<DataFileSpaceReader>(services);
        AssertSingleton<LogSpaceReader>(services);
        AssertSingleton<LogReuseWaitReader>(services);
        AssertSingleton<ActiveTransactionsReader>(services);
        AssertSingleton<SchemaVersionHealthRules>(services);
        AssertSingleton<DataFileSpaceHealthRules>(services);
        AssertSingleton<ApplicationStorageHealthRules>(services);
        AssertSingleton<ConfiguredApplicationsStorageHealthMonitor>(services);
    }

    [Fact]
    public void AddHangfireMonitorServices_ResolvesSameSingletonInstances()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<ConfiguredApplicationsMonitor>();
        var second = provider.GetRequiredService<ConfiguredApplicationsMonitor>();
        var firstReader = provider.GetRequiredService<HangfireStorageReader>();
        var secondReader = provider.GetRequiredService<HangfireStorageReader>();
        var firstStorageHealth = provider.GetRequiredService<ConfiguredApplicationsStorageHealthMonitor>();
        var secondStorageHealth = provider.GetRequiredService<ConfiguredApplicationsStorageHealthMonitor>();
        var firstSchemaReader = provider.GetRequiredService<SchemaVersionReader>();
        var secondSchemaReader = provider.GetRequiredService<SchemaVersionReader>();
        var firstAppRules = provider.GetRequiredService<ApplicationStorageHealthRules>();
        var secondAppRules = provider.GetRequiredService<ApplicationStorageHealthRules>();

        Assert.Same(first, second);
        Assert.Same(firstReader, secondReader);
        Assert.Same(firstStorageHealth, secondStorageHealth);
        Assert.Same(firstSchemaReader, secondSchemaReader);
        Assert.Same(firstAppRules, secondAppRules);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddHangfireMonitorServices();
        return services.BuildServiceProvider();
    }

    private static void AssertSingleton<TService>(IServiceCollection services)
        where TService : class
    {
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(TService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(TService), descriptor.ImplementationType);
    }
}
