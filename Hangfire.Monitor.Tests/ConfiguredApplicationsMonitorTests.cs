using System.Data.Common;
using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Monitoring;
using Hangfire.Monitor.Infrastructure.Storage;

namespace Hangfire.Monitor.Tests;

public class ConfiguredApplicationsMonitorTests
{
    private readonly ApplicationMonitoringRules _rules = new();

    [Fact]
    public void MonitorAll_ThreeApplications_ReturnsOneResultEach()
    {
        var applications = new[]
        {
            App("App A"),
            App("App B"),
            App("App C")
        };

        var monitor = CreateMonitor(app => app.Name switch
        {
            "App A" => new HangfireApplicationFailureInfo(0, null, 1),
            "App B" => new HangfireApplicationFailureInfo(2, new DateTime(2026, 9, 14, 11, 0, 0, DateTimeKind.Utc), 2),
            "App C" => new HangfireApplicationFailureInfo(0, null, 0),
            _ => throw new InvalidOperationException($"Unexpected app: {app.Name}")
        });

        var results = monitor.MonitorAll(applications);

        Assert.Equal(3, results.Count);
        Assert.Equal(["App A", "App B", "App C"], results.Select(r => r.ApplicationName).ToArray());
        Assert.Equal(1, results[0].ServerCount);
        Assert.Equal(2, results[1].ServerCount);
        Assert.Equal(0, results[2].ServerCount);
    }

    [Fact]
    public void MonitorAll_PreservesConfigurationOrder()
    {
        var applications = new[]
        {
            App("B"),
            App("A"),
            App("C")
        };

        var monitor = CreateMonitor(_ => new HangfireApplicationFailureInfo(0, null, 0));

        var results = monitor.MonitorAll(applications);

        Assert.Equal(["B", "A", "C"], results.Select(r => r.ApplicationName).ToArray());
    }

    [Fact]
    public void MonitorAll_MapsOkAndFailedStatuses()
    {
        var lastFailedAt = new DateTime(2026, 9, 14, 11, 42, 37, DateTimeKind.Utc);
        var applications = new[]
        {
            App("App A"),
            App("App B"),
            App("App C")
        };

        var monitor = CreateMonitor(app => app.Name switch
        {
            "App A" => new HangfireApplicationFailureInfo(0, null, 3),
            "App B" => new HangfireApplicationFailureInfo(3, lastFailedAt, 1),
            "App C" => new HangfireApplicationFailureInfo(0, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0),
            _ => throw new InvalidOperationException($"Unexpected app: {app.Name}")
        });

        var results = monitor.MonitorAll(applications);

        Assert.Equal(MonitoringStatus.OK, results[0].Status);
        Assert.Equal(0, results[0].FailedCount);
        Assert.Null(results[0].LastFailedAt);
        Assert.Equal(3, results[0].ServerCount);

        Assert.Equal(MonitoringStatus.FAILED, results[1].Status);
        Assert.Equal(3, results[1].FailedCount);
        Assert.Equal(lastFailedAt, results[1].LastFailedAt);
        Assert.Equal(1, results[1].ServerCount);

        Assert.Equal(MonitoringStatus.OK, results[2].Status);
        Assert.Equal(0, results[2].FailedCount);
        Assert.Null(results[2].LastFailedAt);
        Assert.Equal(0, results[2].ServerCount);
    }

    [Fact]
    public void MonitorAll_ServerCount_DoesNotOverrideStatus()
    {
        var applications = new[]
        {
            App("App A"),
            App("App B")
        };

        var monitor = CreateMonitor(app => app.Name switch
        {
            "App A" => new HangfireApplicationFailureInfo(0, null, 3),
            "App B" => new HangfireApplicationFailureInfo(2, new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc), 3),
            _ => throw new InvalidOperationException($"Unexpected app: {app.Name}")
        });

        var results = monitor.MonitorAll(applications);

        Assert.Equal(MonitoringStatus.OK, results[0].Status);
        Assert.Equal(3, results[0].ServerCount);
        Assert.Equal(MonitoringStatus.FAILED, results[1].Status);
        Assert.Equal(3, results[1].ServerCount);
    }

    [Fact]
    public void MonitorAll_DbException_IsolatesUnavailable_AndContinues()
    {
        var lastFailedAt = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
        var applications = new[]
        {
            App("App A"),
            App("App B"),
            App("App C")
        };

        var monitor = CreateMonitor(app =>
        {
            if (app.Name == "App B")
            {
                throw new StubDbException("storage unavailable");
            }

            return app.Name switch
            {
                "App A" => new HangfireApplicationFailureInfo(0, null, 2),
                "App C" => new HangfireApplicationFailureInfo(1, lastFailedAt, 1),
                _ => throw new InvalidOperationException($"Unexpected app: {app.Name}")
            };
        });

        var results = monitor.MonitorAll(applications);

        Assert.Equal(3, results.Count);

        Assert.Equal("App A", results[0].ApplicationName);
        Assert.Equal(MonitoringStatus.OK, results[0].Status);
        Assert.Equal(2, results[0].ServerCount);

        Assert.Equal("App B", results[1].ApplicationName);
        Assert.Equal(MonitoringStatus.UNAVAILABLE, results[1].Status);
        Assert.Equal(0, results[1].FailedCount);
        Assert.Null(results[1].LastFailedAt);
        Assert.Equal(0, results[1].ServerCount);

        Assert.Equal("App C", results[2].ApplicationName);
        Assert.Equal(MonitoringStatus.FAILED, results[2].Status);
        Assert.Equal(1, results[2].FailedCount);
        Assert.Equal(lastFailedAt, results[2].LastFailedAt);
        Assert.Equal(1, results[2].ServerCount);
    }

    [Fact]
    public void MonitorAll_NonDbException_Propagates()
    {
        var applications = new[]
        {
            App("App A"),
            App("App B")
        };

        var monitor = CreateMonitor(app =>
        {
            if (app.Name == "App A")
            {
                throw new InvalidOperationException("programming error");
            }

            return new HangfireApplicationFailureInfo(0, null, 0);
        });

        var ex = Assert.Throws<InvalidOperationException>(() => monitor.MonitorAll(applications));

        Assert.Equal("programming error", ex.Message);
    }

    [Fact]
    public void MonitorAll_EmptyApplications_ReturnsEmpty()
    {
        var monitor = CreateMonitor(_ => throw new InvalidOperationException("should not be called"));

        var results = monitor.MonitorAll([]);

        Assert.Empty(results);
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

    private sealed class StubDbException : DbException
    {
        public StubDbException(string message)
            : base(message)
        {
        }
    }
}
