using System.Data.Common;
using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Monitoring;
using Hangfire.Monitor.Infrastructure.Storage;
using Hangfire.SqlServer;

namespace Hangfire.Monitor.Tests;

public class ConfiguredApplicationsStorageHealthMonitorTests
{
    private readonly SchemaVersionHealthRules _schemaRules = new();
    private readonly DataFileSpaceHealthRules _dataFileRules = new();
    private readonly ApplicationStorageHealthRules _appRules = new();

    [Fact]
    public void MonitorAll_SingleApplication_ReturnsAllMetrics()
    {
        var schemaVersion = 9;
        var dataMetrics = new DataFileSpaceMetrics(100m, 50m, 50m);
        var logMetrics = new LogSpaceMetrics(200m, 80m, 120m, 40m);
        var reuseMetrics = new LogReuseWaitMetrics(0, "NOTHING", "FULL");
        var txMetrics = new ActiveTransactionMetrics(0, null, null);

        var monitor = CreateMonitor(
            createStorage: _ => CreateStorage(),
            getSchemaVersion: _ => schemaVersion,
            getDataFileSpace: _ => dataMetrics,
            getLogSpace: _ => logMetrics,
            getLogReuseWait: _ => reuseMetrics,
            getActiveTransactions: _ => txMetrics);

        var results = monitor.MonitorAll([App("App A")]);

        var result = Assert.Single(results);
        Assert.Equal("App A", result.ApplicationName);
        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Equal(9, result.Schema.ActualVersion);
        Assert.Equal(StorageHealthStatus.OK, result.Schema.Status);
        Assert.Equal(50.00m, result.DataFiles.UsedPercent);
        Assert.Equal(StorageHealthStatus.OK, result.DataFiles.Status);
        Assert.Equal(logMetrics, result.LogSpace);
        Assert.Equal(reuseMetrics, result.LogReuseWait);
        Assert.Equal(txMetrics, result.ActiveTransactions);
    }

    [Fact]
    public void MonitorAll_PreservesApplicationNameAndMetricPayloads()
    {
        var dataMetrics = new DataFileSpaceMetrics(100m, 50m, 50m);
        var logMetrics = new LogSpaceMetrics(10m, 2m, 8m, 20m);
        var reuseMetrics = new LogReuseWaitMetrics(1, "CHECKPOINT", "SIMPLE");
        var txMetrics = new ActiveTransactionMetrics(
            1,
            new DateTime(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc),
            12);

        var monitor = CreateMonitor(
            _ => CreateStorage(),
            _ => 9,
            _ => dataMetrics,
            _ => logMetrics,
            _ => reuseMetrics,
            _ => txMetrics);

        var result = Assert.Single(monitor.MonitorAll([App("Payments.Worker")]));

        Assert.Equal("Payments.Worker", result.ApplicationName);
        Assert.Equal(9, result.Schema.ActualVersion);
        Assert.Equal(SchemaVersionHealthRules.DefaultExpectedSchemaVersion, result.Schema.ExpectedVersion);
        Assert.Equal(dataMetrics.AllocatedMB, result.DataFiles.AllocatedMB);
        Assert.Equal(dataMetrics.UsedMB, result.DataFiles.UsedMB);
        Assert.Equal(dataMetrics.FreeMB, result.DataFiles.FreeMB);
        Assert.Equal(logMetrics, result.LogSpace);
        Assert.Equal(reuseMetrics, result.LogReuseWait);
        Assert.Equal(txMetrics, result.ActiveTransactions);
    }

    [Fact]
    public void MonitorAll_PreservesConfigurationOrder()
    {
        var monitor = CreateSuccessfulMonitor();

        var results = monitor.MonitorAll([App("B"), App("A"), App("C")]);

        Assert.Equal(["B", "A", "C"], results.Select(r => r.ApplicationName).ToArray());
    }

    [Fact]
    public void MonitorAll_SchemaWarningAndDataOk_ReturnsWarning()
    {
        var monitor = CreateMonitor(
            _ => CreateStorage(),
            _ => 8,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => new LogSpaceMetrics(1m, 0m, 1m, 0m),
            _ => new LogReuseWaitMetrics(0, "NOTHING", "FULL"),
            _ => new ActiveTransactionMetrics(0, null, null));

        var result = Assert.Single(monitor.MonitorAll([App("App1")]));

        Assert.Equal(StorageHealthStatus.WARNING, result.Schema.Status);
        Assert.Equal(StorageHealthStatus.OK, result.DataFiles.Status);
        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
    }

    [Fact]
    public void MonitorAll_SchemaOkAndDataCritical_ReturnsCritical()
    {
        var monitor = CreateMonitor(
            _ => CreateStorage(),
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 95m, 5m),
            _ => new LogSpaceMetrics(1m, 0m, 1m, 0m),
            _ => new LogReuseWaitMetrics(0, "NOTHING", "FULL"),
            _ => new ActiveTransactionMetrics(0, null, null));

        var result = Assert.Single(monitor.MonitorAll([App("App1")]));

        Assert.Equal(StorageHealthStatus.OK, result.Schema.Status);
        Assert.Equal(StorageHealthStatus.CRITICAL, result.DataFiles.Status);
        Assert.Equal(StorageHealthStatus.CRITICAL, result.Status);
    }

    [Fact]
    public void MonitorAll_SchemaDbException_MapsUnavailable_AndContinuesOtherMetrics()
    {
        var dataMetrics = new DataFileSpaceMetrics(100m, 50m, 50m);
        var logMetrics = new LogSpaceMetrics(1m, 0m, 1m, 0m);
        var reuseMetrics = new LogReuseWaitMetrics(0, "NOTHING", "FULL");
        var txMetrics = new ActiveTransactionMetrics(0, null, null);
        var schemaCalls = 0;
        var dataCalls = 0;
        var logCalls = 0;
        var reuseCalls = 0;
        var txCalls = 0;

        var monitor = CreateMonitor(
            _ => CreateStorage(),
            _ =>
            {
                schemaCalls++;
                throw new StubDbException("schema denied");
            },
            _ =>
            {
                dataCalls++;
                return dataMetrics;
            },
            _ =>
            {
                logCalls++;
                return logMetrics;
            },
            _ =>
            {
                reuseCalls++;
                return reuseMetrics;
            },
            _ =>
            {
                txCalls++;
                return txMetrics;
            });

        var result = Assert.Single(monitor.MonitorAll([App("App1")]));

        Assert.Equal(1, schemaCalls);
        Assert.Equal(1, dataCalls);
        Assert.Equal(1, logCalls);
        Assert.Equal(1, reuseCalls);
        Assert.Equal(1, txCalls);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Schema.Status);
        Assert.Null(result.Schema.ActualVersion);
        Assert.Equal(StorageHealthStatus.OK, result.DataFiles.Status);
        Assert.Equal(logMetrics, result.LogSpace);
        Assert.Equal(reuseMetrics, result.LogReuseWait);
        Assert.Equal(txMetrics, result.ActiveTransactions);
        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void MonitorAll_DataFileDbException_MapsUnavailable_AndContinuesOtherMetrics()
    {
        var logMetrics = new LogSpaceMetrics(1m, 0m, 1m, 0m);
        var reuseMetrics = new LogReuseWaitMetrics(0, "NOTHING", "FULL");
        var txMetrics = new ActiveTransactionMetrics(0, null, null);

        var monitor = CreateMonitor(
            _ => CreateStorage(),
            _ => 9,
            _ => throw new StubDbException("data denied"),
            _ => logMetrics,
            _ => reuseMetrics,
            _ => txMetrics);

        var result = Assert.Single(monitor.MonitorAll([App("App1")]));

        Assert.Equal(StorageHealthStatus.OK, result.Schema.Status);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.DataFiles.Status);
        Assert.Null(result.DataFiles.UsedPercent);
        Assert.Equal(logMetrics, result.LogSpace);
        Assert.Equal(reuseMetrics, result.LogReuseWait);
        Assert.Equal(txMetrics, result.ActiveTransactions);
        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void MonitorAll_LogDbException_SetsNull_AndContinuesOtherMetrics()
    {
        var reuseMetrics = new LogReuseWaitMetrics(0, "NOTHING", "FULL");
        var txMetrics = new ActiveTransactionMetrics(0, null, null);
        var reuseCalls = 0;
        var txCalls = 0;

        var monitor = CreateMonitor(
            _ => CreateStorage(),
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => throw new StubDbException("log denied"),
            _ =>
            {
                reuseCalls++;
                return reuseMetrics;
            },
            _ =>
            {
                txCalls++;
                return txMetrics;
            });

        var result = Assert.Single(monitor.MonitorAll([App("App1")]));

        Assert.Equal(1, reuseCalls);
        Assert.Equal(1, txCalls);
        Assert.Null(result.LogSpace);
        Assert.Equal(reuseMetrics, result.LogReuseWait);
        Assert.Equal(txMetrics, result.ActiveTransactions);
        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void MonitorAll_LogReuseDbException_SetsNull_AndContinuesOtherMetrics()
    {
        var logMetrics = new LogSpaceMetrics(1m, 0m, 1m, 0m);
        var txMetrics = new ActiveTransactionMetrics(0, null, null);

        var monitor = CreateMonitor(
            _ => CreateStorage(),
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => logMetrics,
            _ => throw new StubDbException("reuse denied"),
            _ => txMetrics);

        var result = Assert.Single(monitor.MonitorAll([App("App1")]));

        Assert.Equal(logMetrics, result.LogSpace);
        Assert.Null(result.LogReuseWait);
        Assert.Equal(txMetrics, result.ActiveTransactions);
        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void MonitorAll_ActiveTransactionsDbException_SetsNull_AndContinuesOtherMetrics()
    {
        var logMetrics = new LogSpaceMetrics(1m, 0m, 1m, 0m);
        var reuseMetrics = new LogReuseWaitMetrics(0, "NOTHING", "FULL");

        var monitor = CreateMonitor(
            _ => CreateStorage(),
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => logMetrics,
            _ => reuseMetrics,
            _ => throw new StubDbException("tx denied"));

        var result = Assert.Single(monitor.MonitorAll([App("App1")]));

        Assert.Equal(logMetrics, result.LogSpace);
        Assert.Equal(reuseMetrics, result.LogReuseWait);
        Assert.Null(result.ActiveTransactions);
        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void MonitorAll_CreateStorageDbException_ReturnsFullUnavailableRow()
    {
        var metricCalls = 0;

        var monitor = CreateMonitor(
            _ => throw new StubDbException("create failed"),
            _ =>
            {
                metricCalls++;
                return 9;
            },
            _ =>
            {
                metricCalls++;
                return new DataFileSpaceMetrics(100m, 50m, 50m);
            },
            _ =>
            {
                metricCalls++;
                return new LogSpaceMetrics(1m, 0m, 1m, 0m);
            },
            _ =>
            {
                metricCalls++;
                return new LogReuseWaitMetrics(0, "NOTHING", "FULL");
            },
            _ =>
            {
                metricCalls++;
                return new ActiveTransactionMetrics(0, null, null);
            });

        var result = Assert.Single(monitor.MonitorAll([App("App1")]));

        Assert.Equal(0, metricCalls);
        Assert.Equal("App1", result.ApplicationName);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Schema.Status);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.DataFiles.Status);
        Assert.Null(result.LogSpace);
        Assert.Null(result.LogReuseWait);
        Assert.Null(result.ActiveTransactions);
    }

    [Fact]
    public void MonitorAll_OneAppCreateDbException_DoesNotBlockNext()
    {
        var monitor = CreateMonitor(
            app =>
            {
                if (app.Name == "App B")
                {
                    throw new StubDbException("storage unavailable");
                }

                return CreateStorage();
            },
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => new LogSpaceMetrics(1m, 0m, 1m, 0m),
            _ => new LogReuseWaitMetrics(0, "NOTHING", "FULL"),
            _ => new ActiveTransactionMetrics(0, null, null));

        var results = monitor.MonitorAll([App("App A"), App("App B"), App("App C")]);

        Assert.Equal(3, results.Count);
        Assert.Equal(StorageHealthStatus.OK, results[0].Status);
        Assert.Equal("App A", results[0].ApplicationName);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, results[1].Status);
        Assert.Equal("App B", results[1].ApplicationName);
        Assert.Equal(StorageHealthStatus.OK, results[2].Status);
        Assert.Equal("App C", results[2].ApplicationName);
    }

    [Fact]
    public void MonitorAll_NonDbException_Propagates()
    {
        var monitor = CreateMonitor(
            _ => CreateStorage(),
            _ => throw new InvalidOperationException("programming error"),
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => new LogSpaceMetrics(1m, 0m, 1m, 0m),
            _ => new LogReuseWaitMetrics(0, "NOTHING", "FULL"),
            _ => new ActiveTransactionMetrics(0, null, null));

        var ex = Assert.Throws<InvalidOperationException>(() => monitor.MonitorAll([App("App1")]));

        Assert.Equal("programming error", ex.Message);
    }

    [Fact]
    public void MonitorAll_EmptyApplications_ReturnsEmpty()
    {
        var monitor = CreateMonitor(
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"));

        Assert.Empty(monitor.MonitorAll([]));
    }

    [Fact]
    public void MonitorAll_CreatesStorageOncePerApplication()
    {
        var createCount = 0;
        var storage = CreateStorage();

        var monitor = CreateMonitor(
            _ =>
            {
                createCount++;
                return storage;
            },
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => new LogSpaceMetrics(1m, 0m, 1m, 0m),
            _ => new LogReuseWaitMetrics(0, "NOTHING", "FULL"),
            _ => new ActiveTransactionMetrics(0, null, null));

        monitor.MonitorAll([App("App A"), App("App B")]);

        Assert.Equal(2, createCount);
    }

    private ConfiguredApplicationsStorageHealthMonitor CreateSuccessfulMonitor() =>
        CreateMonitor(
            _ => CreateStorage(),
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => new LogSpaceMetrics(1m, 0m, 1m, 0m),
            _ => new LogReuseWaitMetrics(0, "NOTHING", "FULL"),
            _ => new ActiveTransactionMetrics(0, null, null));

    private ConfiguredApplicationsStorageHealthMonitor CreateMonitor(
        Func<HangfireApplicationOptions, SqlServerStorage> createStorage,
        Func<SqlServerStorage, int?> getSchemaVersion,
        Func<SqlServerStorage, DataFileSpaceMetrics> getDataFileSpace,
        Func<SqlServerStorage, LogSpaceMetrics> getLogSpace,
        Func<SqlServerStorage, LogReuseWaitMetrics> getLogReuseWait,
        Func<SqlServerStorage, ActiveTransactionMetrics> getActiveTransactions) =>
        new(
            createStorage,
            getSchemaVersion,
            getDataFileSpace,
            getLogSpace,
            getLogReuseWait,
            getActiveTransactions,
            _schemaRules,
            _dataFileRules,
            _appRules);

    private static SqlServerStorage CreateStorage() =>
        new SqlServerStorageFactory().Create(App("stub-storage"));

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
