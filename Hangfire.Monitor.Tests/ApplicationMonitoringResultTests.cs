using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class ApplicationMonitoringResultTests
{
    [Fact]
    public void CanCreate_Ok_WithZeroFailedCount_AndNullLastFailedAt()
    {
        var result = new ApplicationMonitoringResult(
            ApplicationName: "App1",
            Status: MonitoringStatus.OK,
            FailedCount: 0,
            LastFailedAt: null);

        Assert.Equal("App1", result.ApplicationName);
        Assert.Equal(MonitoringStatus.OK, result.Status);
        Assert.Equal(0, result.FailedCount);
        Assert.Null(result.LastFailedAt);
    }

    [Fact]
    public void CanCreate_Failed_WithFailedCount_AndLastFailedAt()
    {
        var lastFailedAt = new DateTime(2026, 9, 14, 11, 42, 37, DateTimeKind.Utc);

        var result = new ApplicationMonitoringResult(
            ApplicationName: "App2",
            Status: MonitoringStatus.FAILED,
            FailedCount: 3,
            LastFailedAt: lastFailedAt);

        Assert.Equal("App2", result.ApplicationName);
        Assert.Equal(MonitoringStatus.FAILED, result.Status);
        Assert.Equal(3, result.FailedCount);
        Assert.Equal(lastFailedAt, result.LastFailedAt);
    }

    [Fact]
    public void CanCreate_Unavailable()
    {
        var result = new ApplicationMonitoringResult(
            ApplicationName: "App3",
            Status: MonitoringStatus.UNAVAILABLE,
            FailedCount: 0,
            LastFailedAt: null);

        Assert.Equal("App3", result.ApplicationName);
        Assert.Equal(MonitoringStatus.UNAVAILABLE, result.Status);
        Assert.Equal(0, result.FailedCount);
        Assert.Null(result.LastFailedAt);
    }

    [Fact]
    public void ApplicationName_IsPartOfResult()
    {
        var result = new ApplicationMonitoringResult(
            ApplicationName: "Payments.Worker",
            Status: MonitoringStatus.OK,
            FailedCount: 0,
            LastFailedAt: null);

        Assert.Equal("Payments.Worker", result.ApplicationName);
    }
}
