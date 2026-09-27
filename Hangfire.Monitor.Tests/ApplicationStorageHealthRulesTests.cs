using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class ApplicationStorageHealthRulesTests
{
    private readonly ApplicationStorageHealthRules _rules = new();
    private readonly SchemaVersionHealthRules _schemaRules = new();
    private readonly DataFileSpaceHealthRules _dataFileRules = new();

    [Fact]
    public void FromMetrics_HealthyWhenNoHeadroomRisk()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataOk(),
            HeadroomUnlimited(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Contains("No storage capacity issue", result.Diagnosis, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Resolution);
    }

    [Fact]
    public void FromMetrics_SchemaWarning_DoesNotForceCapacityWarning()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaWarning(),
            DataOk(),
            HeadroomUnlimited(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.WARNING, result.Schema.Status);
        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void FromMetrics_HighDataUsedPercent_DoesNotForceWarning()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            _dataFileRules.Evaluate(new DataFileSpaceMetrics(100m, 98m, 2m)),
            HeadroomUnlimited(usedPercent: 98m, freeMb: 2m),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void FromMetrics_HighLogUsedPercent_DoesNotForceWarning()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataOk(),
            HeadroomUnlimited(),
            new LogSpaceMetrics(100m, 99m, 1m, 99m),
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void FromMetrics_ActiveTransactionWaitAlone_DoesNotForceWarning()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataOk(),
            HeadroomUnlimited(),
            logSpace: null,
            new LogReuseWaitMetrics(6, "ACTIVE_TRANSACTION", "SIMPLE"),
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void FromMetrics_ActiveTransactionCountAlone_DoesNotForceWarning()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataOk(),
            HeadroomUnlimited(),
            logSpace: null,
            logReuseWait: null,
            new ActiveTransactionMetrics(5, DateTime.UtcNow, 3600),
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void FromMetrics_ServerCountZero_DoesNotForceWarning()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataOk(),
            HeadroomUnlimited(),
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 0);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Equal(0, result.ServerCount);
    }

    [Fact]
    public void FromMetrics_NullHeadroom_ReturnsHealthy_NotCritical()
    {
        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataOk(),
            dataFileHeadroom: null,
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Contains("headroom", result.Diagnosis, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Resolution);
    }

    [Fact]
    public void FromMetrics_CapacityCeiling_ReturnsWarningWithResolution()
    {
        var headroom = new[]
        {
            new DataFileHeadroomFileMetrics(
                1,
                "HangfireData",
                CurrentSizeMB: 500m,
                UsedMB: 500m,
                FreeMB: 0m,
                UsedPercent: 100m,
                DataFileMaxSizeKind.Limited,
                MaxSizeMB: 500m,
                GrowthMB: 64m,
                GrowthPercent: null,
                IsPercentGrowth: false,
                VolumeMountPoint: "D:\\",
                VolumeTotalGB: 80m,
                VolumeFreeGB: 40m,
                VolumeFreePercent: 50m)
        };

        var result = _rules.FromMetrics(
            "App1",
            SchemaOk(),
            DataOk(),
            headroom,
            logSpace: null,
            logReuseWait: null,
            activeTransactions: null,
            serverCount: 1);

        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
        Assert.NotNull(result.Resolution);
        Assert.Contains("Diagnostic only", result.Resolution!.DiagnosticQueriesSql, StringComparison.Ordinal);
        Assert.Contains("NEVER executes", result.Resolution.CorrectiveQueriesSql, StringComparison.Ordinal);
        Assert.Contains("<new_size_mb>", result.Resolution.CorrectiveQueriesSql, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", result.Resolution.CorrectiveQueriesSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", result.Resolution.CorrectiveQueriesSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromMetrics_PreservesApplicationNameAndMetricPayloads()
    {
        var schema = SchemaOk();
        var dataFiles = DataOk();
        var headroom = HeadroomUnlimited();
        var logSpace = new LogSpaceMetrics(100m, 40m, 60m, 40m);
        var logReuseWait = new LogReuseWaitMetrics(0, "NOTHING", "FULL");
        var activeTransactions = new ActiveTransactionMetrics(1, DateTime.UtcNow, 30);

        var result = _rules.FromMetrics(
            "Payments.Worker",
            schema,
            dataFiles,
            headroom,
            logSpace,
            logReuseWait,
            activeTransactions,
            serverCount: 2);

        Assert.Equal("Payments.Worker", result.ApplicationName);
        Assert.Same(schema, result.Schema);
        Assert.Same(dataFiles, result.DataFiles);
        Assert.Same(headroom, result.DataFileHeadroom);
        Assert.Same(logSpace, result.LogSpace);
        Assert.Same(logReuseWait, result.LogReuseWait);
        Assert.Same(activeTransactions, result.ActiveTransactions);
        Assert.Equal(2, result.ServerCount);
        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void Unavailable_ReturnsFullUnavailableRow_WithFailureReason()
    {
        var result = _rules.Unavailable("App3", "login failed");

        Assert.Equal("App3", result.ApplicationName);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Schema.Status);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.DataFiles.Status);
        Assert.Null(result.DataFileHeadroom);
        Assert.Null(result.LogSpace);
        Assert.Null(result.LogReuseWait);
        Assert.Null(result.ActiveTransactions);
        Assert.Equal(0, result.ServerCount);
        Assert.Equal("login failed", result.FailureReason);
        Assert.Contains("login failed", result.Diagnosis, StringComparison.Ordinal);
        Assert.Null(result.Resolution);
    }

    [Fact]
    public void FromMetrics_WhenSchemaIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _rules.FromMetrics(
            "App1",
            schema: null!,
            DataOk(),
            HeadroomUnlimited(),
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
            HeadroomUnlimited(),
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
            HeadroomUnlimited(),
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
            HeadroomUnlimited(),
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

    private DataFileSpaceHealthResult DataOk() =>
        _dataFileRules.Evaluate(new DataFileSpaceMetrics(100m, 50m, 50m));

    private static IReadOnlyList<DataFileHeadroomFileMetrics> HeadroomUnlimited(
        decimal usedPercent = 50m,
        decimal freeMb = 50m) =>
        [
            new DataFileHeadroomFileMetrics(
                1,
                "data",
                CurrentSizeMB: 100m,
                UsedMB: 100m - freeMb,
                FreeMB: freeMb,
                UsedPercent: usedPercent,
                DataFileMaxSizeKind.Unlimited,
                MaxSizeMB: null,
                GrowthMB: 64m,
                GrowthPercent: null,
                IsPercentGrowth: false,
                VolumeMountPoint: "C:\\",
                VolumeTotalGB: 80m,
                VolumeFreeGB: 40m,
                VolumeFreePercent: 50m)
        ];
}
