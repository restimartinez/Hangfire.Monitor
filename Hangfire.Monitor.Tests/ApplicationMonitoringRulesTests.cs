using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class ApplicationMonitoringRulesTests
{
    private readonly ApplicationMonitoringRules _rules = new();

    [Fact]
    public void FromFailureInfo_ZeroFailedCount_ReturnsOk()
    {
        var result = _rules.FromFailureInfo("App1", failedCount: 0, lastFailedAt: null);

        Assert.Equal(MonitoringStatus.OK, result.Status);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public void FromFailureInfo_ZeroFailedCount_ForcesLastFailedAtNull()
    {
        var result = _rules.FromFailureInfo(
            "App1",
            failedCount: 0,
            lastFailedAt: new DateTime(2026, 9, 14, 11, 42, 37, DateTimeKind.Utc));

        Assert.Equal(MonitoringStatus.OK, result.Status);
        Assert.Null(result.LastFailedAt);
    }

    [Fact]
    public void FromFailureInfo_OneFailedJob_ReturnsFailed()
    {
        var lastFailedAt = new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

        var result = _rules.FromFailureInfo("App2", failedCount: 1, lastFailedAt);

        Assert.Equal(MonitoringStatus.FAILED, result.Status);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(lastFailedAt, result.LastFailedAt);
    }

    [Fact]
    public void FromFailureInfo_MultipleFailedJobs_ReturnsFailed()
    {
        var lastFailedAt = new DateTime(2026, 9, 14, 11, 42, 37, DateTimeKind.Utc);

        var result = _rules.FromFailureInfo("App2", failedCount: 3, lastFailedAt);

        Assert.Equal(MonitoringStatus.FAILED, result.Status);
        Assert.Equal(3, result.FailedCount);
        Assert.Equal(lastFailedAt, result.LastFailedAt);
    }

    [Fact]
    public void FromFailureInfo_WhenFailed_PreservesExactLastFailedAt()
    {
        var lastFailedAt = new DateTime(2026, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc);

        var result = _rules.FromFailureInfo("App2", failedCount: 2, lastFailedAt);

        Assert.Equal(lastFailedAt, result.LastFailedAt);
    }

    [Fact]
    public void FromFailureInfo_WhenFailed_AllowsNullLastFailedAt()
    {
        var result = _rules.FromFailureInfo("App2", failedCount: 2, lastFailedAt: null);

        Assert.Equal(MonitoringStatus.FAILED, result.Status);
        Assert.Null(result.LastFailedAt);
    }

    [Fact]
    public void FromFailureInfo_PreservesApplicationName()
    {
        var result = _rules.FromFailureInfo("Payments.Worker", failedCount: 0, lastFailedAt: null);

        Assert.Equal("Payments.Worker", result.ApplicationName);
    }

    [Fact]
    public void Unavailable_ReturnsUnavailableStatus()
    {
        var result = _rules.Unavailable("App3");

        Assert.Equal("App3", result.ApplicationName);
        Assert.Equal(MonitoringStatus.UNAVAILABLE, result.Status);
        Assert.Equal(0, result.FailedCount);
        Assert.Null(result.LastFailedAt);
    }
}
