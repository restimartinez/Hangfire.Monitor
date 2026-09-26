using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class ApplicationStorageHealthRulesTests
{
    private readonly ApplicationStorageHealthRules _rules = new();
    private readonly SchemaVersionHealthRules _schemaRules = new();
    private readonly DataFileSpaceHealthRules _dataFileRules = new();

    [Fact]
    public void FromMetrics_OkPlusOk_ReturnsOk()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataOk(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void FromMetrics_WarningPlusOk_ReturnsWarning()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaWarning(),
            DataOk(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
    }

    [Fact]
    public void FromMetrics_CriticalPlusOk_ReturnsCritical()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataCritical(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.CRITICAL, result.Status);
    }

    [Fact]
    public void FromMetrics_CriticalPlusWarning_ReturnsCritical()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaWarning(),
            DataCritical(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.CRITICAL, result.Status);
    }

    [Fact]
    public void FromMetrics_UnavailablePlusOk_ReturnsOk()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaUnavailable(),
            DataOk(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void FromMetrics_OkPlusUnavailable_ReturnsOk()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataUnavailable(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void FromMetrics_UnavailablePlusWarning_ReturnsWarning()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaUnavailable(),
            DataWarning(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
    }

    [Fact]
    public void FromMetrics_UnavailablePlusCritical_ReturnsCritical()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaUnavailable(),
            DataCritical(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.CRITICAL, result.Status);
    }

    [Fact]
    public void FromMetrics_UnavailablePlusUnavailable_ReturnsUnavailable()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaUnavailable(),
            DataUnavailable(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
    }

    [Fact]
    public void FromMetrics_PreservesApplicationNameAndMetricPayloads()
    {
        var schema = SchemaOk();
        var dataFiles = DataWarning();
        var logSpace = new LogSpaceMetrics(100m, 40m, 60m, 40m);
        var logReuseWait = new LogReuseWaitMetrics(0, "NOTHING", "FULL");
        var activeTransactions = new ActiveTransactionMetrics(1, DateTime.UtcNow, 30);

        var result = _rules.FromMetrics(
            "Payments.Worker",
            schema,
            dataFiles,
            logSpace,
            logReuseWait,
            activeTransactions,
            serverCount: 2);

        Assert.Equal("Payments.Worker", result.ApplicationName);
        Assert.Same(schema, result.Schema);
        Assert.Same(dataFiles, result.DataFiles);
        Assert.Same(logSpace, result.LogSpace);
        Assert.Same(logReuseWait, result.LogReuseWait);
        Assert.Same(activeTransactions, result.ActiveTransactions);
        Assert.Equal(2, result.ServerCount);
        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
    }

    [Fact]
    public void FromMetrics_AcquisitionOnlyMetrics_DoNotAffectStatus()
    {
        var logSpace = new LogSpaceMetrics(100m, 99m, 1m, 99m);
        var logReuseWait = new LogReuseWaitMetrics(2, "LOG_BACKUP", "FULL");
        var activeTransactions = new ActiveTransactionMetrics(
            5,
            new DateTime(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc),
            3600);

        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataOk(),
            logSpace,
            logReuseWait,
            activeTransactions,
            serverCount: 0);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Same(logSpace, result.LogSpace);
        Assert.Same(logReuseWait, result.LogReuseWait);
        Assert.Same(activeTransactions, result.ActiveTransactions);
        Assert.Equal(0, result.ServerCount);
    }

    [Fact]
    public void FromMetrics_ServerCountZero_DoesNotChangeStatus_WhenOk()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataOk(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 0);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Equal(0, result.ServerCount);
    }

    [Fact]
    public void FromMetrics_ServerCountZero_DoesNotChangeStatus_WhenCritical()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataCritical(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 0);

        Assert.Equal(StorageHealthStatus.CRITICAL, result.Status);
        Assert.Equal(0, result.ServerCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void FromMetrics_PropagatesServerCount(long serverCount)
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataOk(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount);

        Assert.Equal(serverCount, result.ServerCount);
        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void FromMetrics_ServerCountZero_PreservesMetricPayloads()
    {
        var schema = SchemaOk();
        var dataFiles = DataOk();
        var logSpace = new LogSpaceMetrics(100m, 40m, 60m, 40m);
        var logReuseWait = new LogReuseWaitMetrics(0, "NOTHING", "FULL");
        var activeTransactions = new ActiveTransactionMetrics(0, null, null);

        var result = _rules.FromMetrics(
            "App1",
            schema,
            dataFiles,
            logSpace,
            logReuseWait,
            activeTransactions,
            serverCount: 0);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Equal(0, result.ServerCount);
        Assert.Same(schema, result.Schema);
        Assert.Same(dataFiles, result.DataFiles);
        Assert.Same(logSpace, result.LogSpace);
        Assert.Same(logReuseWait, result.LogReuseWait);
        Assert.Same(activeTransactions, result.ActiveTransactions);
    }

    [Fact]
    public void Unavailable_ReturnsFullUnavailableRow()
    {
        var result = _rules.Unavailable("App3");

        Assert.Equal("App3", result.ApplicationName);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Schema.Status);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.DataFiles.Status);
        Assert.Equal(SchemaVersionHealthRules.DefaultExpectedSchemaVersion, result.Schema.ExpectedVersion);
        Assert.Null(result.Schema.ActualVersion);
        Assert.Null(result.DataFiles.UsedPercent);
        Assert.Null(result.LogSpace);
        Assert.Null(result.LogReuseWait);
        Assert.Null(result.ActiveTransactions);
        Assert.Equal(0, result.ServerCount);
    }

    [Fact]
    public void FromMetrics_WhenSchemaIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _rules.FromMetrics(
            "App1",
            schema: null!,
            DataOk(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1));
    }

    [Fact]
    public void FromMetrics_WhenDataFilesIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _rules.FromMetrics(
            "App1",
            SchemaOk(),
            dataFiles: null!,
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1));
    }

    [Fact]
    public void FromMetrics_WhenApplicationNameIsNull_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => _rules.FromMetrics(
            applicationName: null!,
            SchemaOk(),
            DataOk(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1));
    }

    [Fact]
    public void FromMetrics_WhenServerCountIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataOk(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: -1));
    }

    [Fact]
    public void Unavailable_WhenApplicationNameIsNull_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => _rules.Unavailable(null!));
    }

    private SchemaVersionHealthResult SchemaOk() =>
        _schemaRules.Evaluate(9, SchemaVersionHealthRules.DefaultExpectedSchemaVersion);

    private SchemaVersionHealthResult SchemaWarning() =>
        _schemaRules.Evaluate(8, SchemaVersionHealthRules.DefaultExpectedSchemaVersion);

    private SchemaVersionHealthResult SchemaUnavailable() =>
        _schemaRules.Unavailable(SchemaVersionHealthRules.DefaultExpectedSchemaVersion);

    private DataFileSpaceHealthResult DataOk() =>
        _dataFileRules.Evaluate(new DataFileSpaceMetrics(100m, 50m, 50m));

    private DataFileSpaceHealthResult DataWarning() =>
        _dataFileRules.Evaluate(new DataFileSpaceMetrics(100m, 85m, 15m));

    private DataFileSpaceHealthResult DataCritical() =>
        _dataFileRules.Evaluate(new DataFileSpaceMetrics(100m, 95m, 5m));

    private DataFileSpaceHealthResult DataUnavailable() =>
        _dataFileRules.Unavailable();
}
