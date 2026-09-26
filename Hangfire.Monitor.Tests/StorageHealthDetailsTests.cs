using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class StorageHealthDetailsTests
{
    [Fact]
    public void From_MapsAllExistingResultValues()
    {
        var schema = new SchemaVersionHealthResult(
            9,
            9,
            StorageHealthStatus.OK,
            "ok",
            "none");
        var dataFiles = new DataFileSpaceHealthResult(
            100m,
            58.8m,
            41.2m,
            58.80m,
            StorageHealthStatus.OK,
            "ok",
            "none");
        var log = new LogSpaceMetrics(200m, 20m, 180m, 10m);
        var logReuse = new LogReuseWaitMetrics(2, "LOG_BACKUP", "FULL");
        var transactions = new ActiveTransactionMetrics(
            1,
            new DateTime(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc),
            42);
        var result = new ApplicationStorageHealthResult(
            "Payments.Worker",
            StorageHealthStatus.OK,
            schema,
            dataFiles,
            log,
            logReuse,
            transactions,
            ServerCount: 4);

        var details = StorageHealthDetails.From(result);

        Assert.Equal("Payments.Worker", details.ApplicationName);
        Assert.Equal(4, details.ServerCount);
        Assert.Same(dataFiles, details.DataFiles);
        Assert.Equal(log, details.Log);
        Assert.Equal(logReuse, details.LogReuse);
        Assert.Equal(transactions, details.ActiveTransactions);
        Assert.Equal(StorageHealthStatus.OK, details.Status);
        Assert.False(details.IsUnavailable);
    }

    [Fact]
    public void From_WhenUnavailable_SetsIsUnavailable()
    {
        var result = new ApplicationStorageHealthRules().Unavailable("Offline.App");

        var details = StorageHealthDetails.From(result);

        Assert.True(details.IsUnavailable);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, details.Status);
        Assert.Null(details.Log);
        Assert.Null(details.LogReuse);
        Assert.Null(details.ActiveTransactions);
    }
}
