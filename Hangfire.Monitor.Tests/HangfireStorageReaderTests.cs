using Hangfire;
using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Storage;
using Hangfire.SqlServer;
using Hangfire.Storage.Monitoring;

namespace Hangfire.Monitor.Tests;

public class HangfireStorageReaderTests
{
    private const string ExampleConnectionString =
        "Server=localhost;Database=ExampleHangfire;Trusted_Connection=True;TrustServerCertificate=True;";

    [Fact]
    public void Read_CreatesStorageOnce_AndUsesSameInstanceForBothReads()
    {
        var application = CreateApplication();
        var storage = new SqlServerStorageFactory().Create(application);

        var createCount = 0;
        SqlServerStorage? storageSeenByStatistics = null;
        SqlServerStorage? storageSeenByLastFailedAt = null;

        var reader = new HangfireStorageReader(
            _ =>
            {
                createCount++;
                return storage;
            },
            s =>
            {
                storageSeenByStatistics = s;
                return new StatisticsDto { Failed = 5, Servers = 2 };
            },
            s =>
            {
                storageSeenByLastFailedAt = s;
                return new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
            });

        var result = reader.Read(application);

        Assert.Equal(1, createCount);
        Assert.Same(storage, storageSeenByStatistics);
        Assert.Same(storage, storageSeenByLastFailedAt);
        Assert.Same(storageSeenByStatistics, storageSeenByLastFailedAt);
        Assert.Equal(5, result.FailedCount);
        Assert.Equal(2, result.ServerCount);
        Assert.Equal(new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc), result.LastFailedAt);
    }

    [Fact]
    public void Read_ReturnsFailedCount_ServerCount_AndLastFailedAt_FromSingleStatistics()
    {
        var application = CreateApplication();
        var storage = new SqlServerStorageFactory().Create(application);
        var lastFailedAt = new DateTime(2025, 3, 1, 8, 30, 0, DateTimeKind.Utc);
        var statisticsCalls = 0;

        var reader = new HangfireStorageReader(
            _ => storage,
            _ =>
            {
                statisticsCalls++;
                return new StatisticsDto { Failed = 2, Servers = 3 };
            },
            _ => lastFailedAt);

        var result = reader.Read(application);

        Assert.Equal(1, statisticsCalls);
        Assert.Equal(2, result.FailedCount);
        Assert.Equal(3, result.ServerCount);
        Assert.Equal(lastFailedAt, result.LastFailedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void Read_MapsStatisticsServers_ToServerCount(long servers)
    {
        var application = CreateApplication();
        var storage = new SqlServerStorageFactory().Create(application);

        var reader = new HangfireStorageReader(
            _ => storage,
            _ => new StatisticsDto { Failed = 0, Servers = servers },
            _ => null);

        var result = reader.Read(application);

        Assert.Equal(servers, result.ServerCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Null(result.LastFailedAt);
    }

    [Fact]
    public void Read_ReturnsNullLastFailedAt_WhenNoFailedJobs()
    {
        var application = CreateApplication();
        var storage = new SqlServerStorageFactory().Create(application);

        var reader = new HangfireStorageReader(
            _ => storage,
            _ => new StatisticsDto { Failed = 0, Servers = 1 },
            _ => null);

        var result = reader.Read(application);

        Assert.Equal(0, result.FailedCount);
        Assert.Equal(1, result.ServerCount);
        Assert.Null(result.LastFailedAt);
    }

    [Fact]
    public void Read_DoesNotSetJobStorageCurrent()
    {
        var application = CreateApplication();
        var storage = new SqlServerStorageFactory().Create(application);

        var reader = new HangfireStorageReader(
            _ => storage,
            _ => new StatisticsDto { Failed = 1, Servers = 0 },
            _ => new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        _ = reader.Read(application);

        Assert.Throws<InvalidOperationException>(() => _ = JobStorage.Current);
    }

    private static HangfireApplicationOptions CreateApplication() =>
        new()
        {
            Name = "Example",
            ConnectionString = ExampleConnectionString,
            Schema = HangfireApplicationOptions.DefaultSchema
        };
}
