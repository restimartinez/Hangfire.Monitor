using Hangfire;
using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Storage;
using Hangfire.SqlServer;

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
        SqlServerStorage? storageSeenByCount = null;
        SqlServerStorage? storageSeenByLastFailedAt = null;

        var reader = new HangfireStorageReader(
            _ =>
            {
                createCount++;
                return storage;
            },
            s =>
            {
                storageSeenByCount = s;
                return 5;
            },
            s =>
            {
                storageSeenByLastFailedAt = s;
                return new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
            });

        var result = reader.Read(application);

        Assert.Equal(1, createCount);
        Assert.Same(storage, storageSeenByCount);
        Assert.Same(storage, storageSeenByLastFailedAt);
        Assert.Same(storageSeenByCount, storageSeenByLastFailedAt);
        Assert.Equal(5, result.FailedCount);
        Assert.Equal(new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc), result.LastFailedAt);
    }

    [Fact]
    public void Read_ReturnsBothFailedCount_AndLastFailedAt()
    {
        var application = CreateApplication();
        var storage = new SqlServerStorageFactory().Create(application);
        var lastFailedAt = new DateTime(2025, 3, 1, 8, 30, 0, DateTimeKind.Utc);

        var reader = new HangfireStorageReader(
            _ => storage,
            _ => 2,
            _ => lastFailedAt);

        var result = reader.Read(application);

        Assert.Equal(2, result.FailedCount);
        Assert.Equal(lastFailedAt, result.LastFailedAt);
    }

    [Fact]
    public void Read_ReturnsNullLastFailedAt_WhenNoFailedJobs()
    {
        var application = CreateApplication();
        var storage = new SqlServerStorageFactory().Create(application);

        var reader = new HangfireStorageReader(
            _ => storage,
            _ => 0,
            _ => null);

        var result = reader.Read(application);

        Assert.Equal(0, result.FailedCount);
        Assert.Null(result.LastFailedAt);
    }

    [Fact]
    public void Read_DoesNotSetJobStorageCurrent()
    {
        var application = CreateApplication();
        var storage = new SqlServerStorageFactory().Create(application);

        var reader = new HangfireStorageReader(
            _ => storage,
            _ => 1,
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
