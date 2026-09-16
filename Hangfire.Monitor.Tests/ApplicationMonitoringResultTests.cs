using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class ApplicationMonitoringResultTests
{
    [Fact]
    public void CanCreate_Ok_WithZeroFailedCount_AndNullLastFailedAt()
    {
        var result = new ApplicationMonitoringResult(
            ApplicationName: "App",
            Status: MonitoringStatus.OK,
            FailedCount: 0,
            LastFailedAt: null,
            ServerCount: 2);

        Assert.Equal("App", result.ApplicationName);
        Assert.Equal(MonitoringStatus.OK, result.Status);
        Assert.Equal(0, result.FailedCount);
        Assert.Null(result.LastFailedAt);
        Assert.Equal(2, result.ServerCount);
    }

    [Fact]
    public void CanCreate_Failed_WithFailedCount_AndLastFailedAt()
    {
        var lastFailedAt = new DateTime(2026, 9, 14, 11, 42, 37, DateTimeKind.Utc);

        var result = new ApplicationMonitoringResult(
            ApplicationName: "App",
            Status: MonitoringStatus.FAILED,
            FailedCount: 3,
            LastFailedAt: lastFailedAt,
            ServerCount: 1);

        Assert.Equal("App", result.ApplicationName);
        Assert.Equal(MonitoringStatus.FAILED, result.Status);
        Assert.Equal(3, result.FailedCount);
        Assert.Equal(lastFailedAt, result.LastFailedAt);
        Assert.Equal(1, result.ServerCount);
    }

    [Fact]
    public void CanCreate_Unavailable()
    {
        var result = new ApplicationMonitoringResult(
            ApplicationName: "App",
            Status: MonitoringStatus.UNAVAILABLE,
            FailedCount: 0,
            LastFailedAt: null,
            ServerCount: 0);

        Assert.Equal("App", result.ApplicationName);
        Assert.Equal(MonitoringStatus.UNAVAILABLE, result.Status);
        Assert.Equal(0, result.FailedCount);
        Assert.Null(result.LastFailedAt);
        Assert.Equal(0, result.ServerCount);
    }

    [Fact]
    public void CanCreate_WithZeroServerCount()
    {
        var result = new ApplicationMonitoringResult(
            ApplicationName: "App",
            Status: MonitoringStatus.OK,
            FailedCount: 0,
            LastFailedAt: null,
            ServerCount: 0);

        Assert.Equal(0, result.ServerCount);
    }
}
