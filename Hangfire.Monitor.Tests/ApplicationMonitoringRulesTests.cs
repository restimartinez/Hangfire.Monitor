using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class ApplicationMonitoringRulesTests
{
    private readonly ApplicationMonitoringRules _rules = new();

    [Fact]
    public void FromFailureInfo_ZeroFailedCount_ReturnsOk()
    {
        var result = _rules.FromFailureInfo("App1", failedCount: 0, lastFailedAt: null, serverCount: 0);

        Assert.Equal(MonitoringStatus.OK, result.Status);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.ServerCount);
    }

    [Fact]
    public void FromFailureInfo_ZeroFailedCount_ForcesLastFailedAtNull()
    {
        var result = _rules.FromFailureInfo(
            "App1",
            failedCount: 0,
            lastFailedAt: new DateTime(2026, 9, 14, 11, 42, 37, DateTimeKind.Utc),
            serverCount: 1);

        Assert.Equal(MonitoringStatus.OK, result.Status);
        Assert.Null(result.LastFailedAt);
        Assert.Equal(1, result.ServerCount);
    }

    [Fact]
    public void FromFailureInfo_OneFailedJob_ReturnsFailed()
    {
        var lastFailedAt = new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

        var result = _rules.FromFailureInfo("App2", failedCount: 1, lastFailedAt, serverCount: 0);

        Assert.Equal(MonitoringStatus.FAILED, result.Status);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(lastFailedAt, result.LastFailedAt);
        Assert.Equal(0, result.ServerCount);
    }

    [Fact]
    public void FromFailureInfo_MultipleFailedJobs_ReturnsFailed()
    {
        var lastFailedAt = new DateTime(2026, 9, 14, 11, 42, 37, DateTimeKind.Utc);

        var result = _rules.FromFailureInfo("App2", failedCount: 3, lastFailedAt, serverCount: 2);

        Assert.Equal(MonitoringStatus.FAILED, result.Status);
        Assert.Equal(3, result.FailedCount);
        Assert.Equal(lastFailedAt, result.LastFailedAt);
        Assert.Equal(2, result.ServerCount);
    }

    [Fact]
    public void FromFailureInfo_WhenFailed_PreservesExactLastFailedAt()
    {
        var lastFailedAt = new DateTime(2026, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc);

        var result = _rules.FromFailureInfo("App2", failedCount: 2, lastFailedAt, serverCount: 1);

        Assert.Equal(lastFailedAt, result.LastFailedAt);
    }

    [Fact]
    public void FromFailureInfo_WhenFailed_AllowsNullLastFailedAt()
    {
        var result = _rules.FromFailureInfo("App2", failedCount: 2, lastFailedAt: null, serverCount: 0);

        Assert.Equal(MonitoringStatus.FAILED, result.Status);
        Assert.Null(result.LastFailedAt);
    }

    [Fact]
    public void FromFailureInfo_PreservesApplicationName()
    {
        var result = _rules.FromFailureInfo("Payments.Worker", failedCount: 0, lastFailedAt: null, serverCount: 0);

        Assert.Equal("Payments.Worker", result.ApplicationName);
    }

    [Fact]
    public void FromFailureInfo_ServerCount_DoesNotChangeStatus_WhenOk()
    {
        var result = _rules.FromFailureInfo("App1", failedCount: 0, lastFailedAt: null, serverCount: 3);

        Assert.Equal(MonitoringStatus.OK, result.Status);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(3, result.ServerCount);
    }

    [Fact]
    public void FromFailureInfo_ServerCount_DoesNotChangeStatus_WhenFailed()
    {
        var lastFailedAt = new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

        var result = _rules.FromFailureInfo("App2", failedCount: 2, lastFailedAt, serverCount: 3);

        Assert.Equal(MonitoringStatus.FAILED, result.Status);
        Assert.Equal(2, result.FailedCount);
        Assert.Equal(3, result.ServerCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void FromFailureInfo_PropagatesServerCount(long serverCount)
    {
        var result = _rules.FromFailureInfo("App1", failedCount: 0, lastFailedAt: null, serverCount);

        Assert.Equal(serverCount, result.ServerCount);
    }

    [Fact]
    public void Unavailable_ReturnsUnavailableStatus()
    {
        var result = _rules.Unavailable("App3");

        Assert.Equal("App3", result.ApplicationName);
        Assert.Equal(MonitoringStatus.UNAVAILABLE, result.Status);
        Assert.Equal(0, result.FailedCount);
        Assert.Null(result.LastFailedAt);
        Assert.Equal(0, result.ServerCount);
    }
}
