using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Monitoring;
using Hangfire.Monitor.Infrastructure.Storage;
using Hangfire.Monitor.Web.Pages.StorageHealth;
using Hangfire.SqlServer;
using Microsoft.Extensions.Options;

namespace Hangfire.Monitor.Tests;

public class StorageHealthIndexModelTests
{
    private readonly SchemaVersionHealthRules _schemaRules = new();
    private readonly DataFileSpaceHealthRules _dataFileRules = new();
    private readonly ApplicationStorageHealthRules _appRules = new();

    [Fact]
    public void OnGet_WithConfiguredApplications_ExposesMonitorResults()
    {
        var logSpace = new LogSpaceMetrics(100m, 40m, 60m, 40m);
        var logReuse = new LogReuseWaitMetrics(0, "NOTHING", "FULL");
        var transactions = new ActiveTransactionMetrics(0, null, null);
        var applications = new List<HangfireApplicationOptions>
        {
            App("App A"),
            App("App B")
        };
        var options = Options.Create(new HangfireMonitorOptions { Applications = applications });

        var monitor = CreateMonitor(
            _ => CreateStorage(),
            app => app.Name == "App A" ? 9 : 8,
            app => app.Name == "App A"
                ? new DataFileSpaceMetrics(100m, 50m, 50m)
                : new DataFileSpaceMetrics(100m, 95m, 5m),
            _ => logSpace,
            _ => logReuse,
            _ => transactions);

        var model = new IndexModel(monitor, options);

        model.OnGet();

        Assert.Equal(2, model.Results.Count);

        Assert.Equal("App A", model.Results[0].ApplicationName);
        Assert.Equal(StorageHealthStatus.OK, model.Results[0].Status);
        Assert.Equal(9, model.Results[0].Schema.ActualVersion);
        Assert.Equal(50.00m, model.Results[0].DataFiles.UsedPercent);
        Assert.Equal(logSpace, model.Results[0].LogSpace);
        Assert.Equal(logReuse, model.Results[0].LogReuseWait);
        Assert.Equal(transactions, model.Results[0].ActiveTransactions);

        Assert.Equal("App B", model.Results[1].ApplicationName);
        Assert.Equal(StorageHealthStatus.CRITICAL, model.Results[1].Status);
        Assert.Equal(8, model.Results[1].Schema.ActualVersion);
        Assert.Equal(StorageHealthStatus.WARNING, model.Results[1].Schema.Status);
        Assert.Equal(StorageHealthStatus.CRITICAL, model.Results[1].DataFiles.Status);
    }

    [Fact]
    public void OnGet_PreservesMonitorOrder()
    {
        var applications = new List<HangfireApplicationOptions>
        {
            App("B"),
            App("A"),
            App("C")
        };
        var options = Options.Create(new HangfireMonitorOptions { Applications = applications });
        var monitor = CreateSuccessfulMonitor();

        var model = new IndexModel(monitor, options);

        model.OnGet();

        Assert.Equal(["B", "A", "C"], model.Results.Select(r => r.ApplicationName).ToArray());
    }

    [Fact]
    public void OnGet_PassesConfiguredApplications_FromOptions_ToMonitor()
    {
        var applications = new List<HangfireApplicationOptions>
        {
            App("App A"),
            App("App B")
        };
        var options = Options.Create(new HangfireMonitorOptions { Applications = applications });

        var received = new List<HangfireApplicationOptions>();
        var monitor = CreateMonitor(
            app =>
            {
                received.Add(app);
                return CreateStorage();
            },
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => new LogSpaceMetrics(1m, 0m, 1m, 0m),
            _ => new LogReuseWaitMetrics(0, "NOTHING", "FULL"),
            _ => new ActiveTransactionMetrics(0, null, null));

        var model = new IndexModel(monitor, options);

        model.OnGet();

        Assert.Equal(2, received.Count);
        Assert.Same(applications[0], received[0]);
        Assert.Same(applications[1], received[1]);
        Assert.Equal(["App A", "App B"], model.Results.Select(r => r.ApplicationName).ToArray());
    }

    [Fact]
    public void OnGet_WithEmptyApplications_ExposesEmptyResults()
    {
        var options = Options.Create(new HangfireMonitorOptions { Applications = [] });
        var monitor = CreateMonitor(
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"));

        var model = new IndexModel(monitor, options);

        model.OnGet();

        Assert.Empty(model.Results);
    }

    [Fact]
    public void OnGet_ExposesMonitorResults_WithoutTransformingPayloads()
    {
        var dataMetrics = new DataFileSpaceMetrics(100m, 85m, 15m);
        var logSpace = new LogSpaceMetrics(200m, 130m, 70m, 65m);
        var logReuse = new LogReuseWaitMetrics(2, "LOG_BACKUP", "FULL");
        var transactions = new ActiveTransactionMetrics(
            2,
            new DateTime(2026, 9, 26, 9, 0, 0, DateTimeKind.Utc),
            90);

        var options = Options.Create(new HangfireMonitorOptions
        {
            Applications = [App("Payments.Worker")]
        });

        var monitor = CreateMonitor(
            _ => CreateStorage(),
            _ => 9,
            _ => dataMetrics,
            _ => logSpace,
            _ => logReuse,
            _ => transactions);

        var model = new IndexModel(monitor, options);

        model.OnGet();

        var result = Assert.Single(model.Results);
        Assert.Equal(dataMetrics.AllocatedMB, result.DataFiles.AllocatedMB);
        Assert.Equal(dataMetrics.UsedMB, result.DataFiles.UsedMB);
        Assert.Equal(dataMetrics.FreeMB, result.DataFiles.FreeMB);
        Assert.Equal(85.00m, result.DataFiles.UsedPercent);
        Assert.Equal(StorageHealthStatus.WARNING, result.DataFiles.Status);
        Assert.Equal(logSpace, result.LogSpace);
        Assert.Equal(logReuse, result.LogReuseWait);
        Assert.Equal(transactions, result.ActiveTransactions);
    }

    private ConfiguredApplicationsStorageHealthMonitor CreateSuccessfulMonitor() =>
        CreateMonitor(
            _ => CreateStorage(),
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => new LogSpaceMetrics(1m, 0m, 1m, 0m),
            _ => new LogReuseWaitMetrics(0, "NOTHING", "FULL"),
            _ => new ActiveTransactionMetrics(0, null, null));

    private ConfiguredApplicationsStorageHealthMonitor CreateMonitor(
        Func<HangfireApplicationOptions, SqlServerStorage> createStorage,
        Func<HangfireApplicationOptions, int?> getSchemaVersionForApp,
        Func<HangfireApplicationOptions, DataFileSpaceMetrics> getDataFileSpaceForApp,
        Func<SqlServerStorage, LogSpaceMetrics> getLogSpace,
        Func<SqlServerStorage, LogReuseWaitMetrics> getLogReuseWait,
        Func<SqlServerStorage, ActiveTransactionMetrics> getActiveTransactions)
    {
        // Bind per-app metric stubs through createStorage callback side channel:
        // createStorage receives the app; metric funcs only receive storage.
        // Track last app via closure when createStorage runs before metric reads.
        HangfireApplicationOptions? current = null;

        return new ConfiguredApplicationsStorageHealthMonitor(
            app =>
            {
                current = app;
                return createStorage(app);
            },
            _ => getSchemaVersionForApp(current!),
            _ => getDataFileSpaceForApp(current!),
            getLogSpace,
            getLogReuseWait,
            getActiveTransactions,
            _schemaRules,
            _dataFileRules,
            _appRules);
    }

    private static SqlServerStorage CreateStorage() =>
        new SqlServerStorageFactory().Create(App("stub-storage"));

    private static HangfireApplicationOptions App(string name) =>
        new()
        {
            Name = name,
            ConnectionString = "Server=localhost;Database=Example;Trusted_Connection=True;",
            Schema = HangfireApplicationOptions.DefaultSchema
        };
}
