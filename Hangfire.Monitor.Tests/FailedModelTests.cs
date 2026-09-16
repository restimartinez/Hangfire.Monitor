using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Monitoring;
using Hangfire.Monitor.Infrastructure.Storage;
using Hangfire.Monitor.Web.Pages.Jobs;
using Microsoft.Extensions.Options;

namespace Hangfire.Monitor.Tests;

public class FailedModelTests
{
    private readonly ApplicationMonitoringRules _rules = new();

    [Fact]
    public void OnGet_WithConfiguredApplications_ExposesMonitorResults()
    {
        var lastFailedAt = new DateTime(2026, 9, 14, 11, 0, 0, DateTimeKind.Utc);
        var applications = new List<HangfireApplicationOptions>
        {
            App("App A"),
            App("App B")
        };
        var options = Options.Create(new HangfireMonitorOptions { Applications = applications });

        var monitor = CreateMonitor(app => app.Name switch
        {
            "App A" => new HangfireApplicationFailureInfo(0, null, 3),
            "App B" => new HangfireApplicationFailureInfo(2, lastFailedAt, 1),
            _ => throw new InvalidOperationException($"Unexpected app: {app.Name}")
        });

        var model = new FailedModel(monitor, options);

        model.OnGet();

        Assert.Equal(2, model.Results.Count);

        Assert.Equal("App A", model.Results[0].ApplicationName);
        Assert.Equal(MonitoringStatus.OK, model.Results[0].Status);
        Assert.Equal(0, model.Results[0].FailedCount);
        Assert.Null(model.Results[0].LastFailedAt);
        Assert.Equal(3, model.Results[0].ServerCount);

        Assert.Equal("App B", model.Results[1].ApplicationName);
        Assert.Equal(MonitoringStatus.FAILED, model.Results[1].Status);
        Assert.Equal(2, model.Results[1].FailedCount);
        Assert.Equal(lastFailedAt, model.Results[1].LastFailedAt);
        Assert.Equal(1, model.Results[1].ServerCount);
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
        var monitor = CreateMonitor(app =>
        {
            received.Add(app);
            return new HangfireApplicationFailureInfo(0, null, 0);
        });

        var model = new FailedModel(monitor, options);

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
        var monitor = CreateMonitor(_ => throw new InvalidOperationException("should not be called"));

        var model = new FailedModel(monitor, options);

        model.OnGet();

        Assert.Empty(model.Results);
    }

    [Fact]
    public void OnGet_ExposesServerCount_OnResults()
    {
        var applications = new List<HangfireApplicationOptions>
        {
            App("App A"),
            App("App B"),
            App("App C")
        };
        var options = Options.Create(new HangfireMonitorOptions { Applications = applications });

        var monitor = CreateMonitor(app => app.Name switch
        {
            "App A" => new HangfireApplicationFailureInfo(0, null, 0),
            "App B" => new HangfireApplicationFailureInfo(0, null, 1),
            "App C" => new HangfireApplicationFailureInfo(1, new DateTime(2026, 9, 14, 11, 0, 0, DateTimeKind.Utc), 4),
            _ => throw new InvalidOperationException($"Unexpected app: {app.Name}")
        });

        var model = new FailedModel(monitor, options);

        model.OnGet();

        Assert.Equal(0, model.Results[0].ServerCount);
        Assert.Equal(1, model.Results[1].ServerCount);
        Assert.Equal(4, model.Results[2].ServerCount);
    }

    private ConfiguredApplicationsMonitor CreateMonitor(
        Func<HangfireApplicationOptions, HangfireApplicationFailureInfo> readFailureInfo) =>
        new(readFailureInfo, _rules);

    private static HangfireApplicationOptions App(string name) =>
        new()
        {
            Name = name,
            ConnectionString = "Server=localhost;Database=Example;Trusted_Connection=True;",
            Schema = HangfireApplicationOptions.DefaultSchema
        };
}
