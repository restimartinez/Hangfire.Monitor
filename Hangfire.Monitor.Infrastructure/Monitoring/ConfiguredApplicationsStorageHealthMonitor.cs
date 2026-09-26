using System.Data.Common;
using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Storage;
using Hangfire.SqlServer;

namespace Hangfire.Monitor.Infrastructure.Monitoring;

/// <summary>
/// Evaluates storage health for every configured Hangfire application independently and
/// returns one <see cref="ApplicationStorageHealthResult"/> per application in configuration order.
/// </summary>
public class ConfiguredApplicationsStorageHealthMonitor
{
    private readonly Func<HangfireApplicationOptions, SqlServerStorage> _createStorage;
    private readonly Func<SqlServerStorage, int?> _getSchemaVersion;
    private readonly Func<SqlServerStorage, DataFileSpaceMetrics> _getDataFileSpace;
    private readonly Func<SqlServerStorage, LogSpaceMetrics> _getLogSpace;
    private readonly Func<SqlServerStorage, LogReuseWaitMetrics> _getLogReuseWait;
    private readonly Func<SqlServerStorage, ActiveTransactionMetrics> _getActiveTransactions;
    private readonly Func<SqlServerStorage, long> _getServerCount;
    private readonly SchemaVersionHealthRules _schemaRules;
    private readonly DataFileSpaceHealthRules _dataFileRules;
    private readonly ApplicationStorageHealthRules _appRules;

    public ConfiguredApplicationsStorageHealthMonitor(
        SqlServerStorageFactory storageFactory,
        SchemaVersionReader schemaVersionReader,
        DataFileSpaceReader dataFileSpaceReader,
        LogSpaceReader logSpaceReader,
        LogReuseWaitReader logReuseWaitReader,
        ActiveTransactionsReader activeTransactionsReader,
        FailedJobCountReader failedJobCountReader,
        SchemaVersionHealthRules schemaVersionHealthRules,
        DataFileSpaceHealthRules dataFileSpaceHealthRules,
        ApplicationStorageHealthRules applicationStorageHealthRules)
    {
        ArgumentNullException.ThrowIfNull(storageFactory);
        ArgumentNullException.ThrowIfNull(schemaVersionReader);
        ArgumentNullException.ThrowIfNull(dataFileSpaceReader);
        ArgumentNullException.ThrowIfNull(logSpaceReader);
        ArgumentNullException.ThrowIfNull(logReuseWaitReader);
        ArgumentNullException.ThrowIfNull(activeTransactionsReader);
        ArgumentNullException.ThrowIfNull(failedJobCountReader);
        ArgumentNullException.ThrowIfNull(schemaVersionHealthRules);
        ArgumentNullException.ThrowIfNull(dataFileSpaceHealthRules);
        ArgumentNullException.ThrowIfNull(applicationStorageHealthRules);

        _createStorage = storageFactory.Create;
        _getSchemaVersion = schemaVersionReader.GetSchemaVersion;
        _getDataFileSpace = dataFileSpaceReader.GetDataFileSpace;
        _getLogSpace = logSpaceReader.GetLogSpace;
        _getLogReuseWait = logReuseWaitReader.GetLogReuseWait;
        _getActiveTransactions = activeTransactionsReader.GetActiveTransactions;
        _getServerCount = storage => failedJobCountReader.GetStatistics(storage).Servers;
        _schemaRules = schemaVersionHealthRules;
        _dataFileRules = dataFileSpaceHealthRules;
        _appRules = applicationStorageHealthRules;
    }

    /// <summary>
    /// Test seam: substitute storage creation and metric reads without a live SQL Server.
    /// </summary>
    internal ConfiguredApplicationsStorageHealthMonitor(
        Func<HangfireApplicationOptions, SqlServerStorage> createStorage,
        Func<SqlServerStorage, int?> getSchemaVersion,
        Func<SqlServerStorage, DataFileSpaceMetrics> getDataFileSpace,
        Func<SqlServerStorage, LogSpaceMetrics> getLogSpace,
        Func<SqlServerStorage, LogReuseWaitMetrics> getLogReuseWait,
        Func<SqlServerStorage, ActiveTransactionMetrics> getActiveTransactions,
        Func<SqlServerStorage, long> getServerCount,
        SchemaVersionHealthRules schemaVersionHealthRules,
        DataFileSpaceHealthRules dataFileSpaceHealthRules,
        ApplicationStorageHealthRules applicationStorageHealthRules)
    {
        _createStorage = createStorage ?? throw new ArgumentNullException(nameof(createStorage));
        _getSchemaVersion = getSchemaVersion ?? throw new ArgumentNullException(nameof(getSchemaVersion));
        _getDataFileSpace = getDataFileSpace ?? throw new ArgumentNullException(nameof(getDataFileSpace));
        _getLogSpace = getLogSpace ?? throw new ArgumentNullException(nameof(getLogSpace));
        _getLogReuseWait = getLogReuseWait ?? throw new ArgumentNullException(nameof(getLogReuseWait));
        _getActiveTransactions = getActiveTransactions
            ?? throw new ArgumentNullException(nameof(getActiveTransactions));
        _getServerCount = getServerCount ?? throw new ArgumentNullException(nameof(getServerCount));
        _schemaRules = schemaVersionHealthRules
            ?? throw new ArgumentNullException(nameof(schemaVersionHealthRules));
        _dataFileRules = dataFileSpaceHealthRules
            ?? throw new ArgumentNullException(nameof(dataFileSpaceHealthRules));
        _appRules = applicationStorageHealthRules
            ?? throw new ArgumentNullException(nameof(applicationStorageHealthRules));
    }

    /// <summary>
    /// Monitors each application in <paramref name="applications"/> order.
    /// <see cref="DbException"/> when creating storage becomes a full-row
    /// <see cref="StorageHealthStatus.UNAVAILABLE"/> for that app only.
    /// <see cref="DbException"/> from an individual metric is isolated to that metric;
    /// other metrics for the same app continue. Non-<see cref="DbException"/> errors propagate.
    /// </summary>
    public IReadOnlyList<ApplicationStorageHealthResult> MonitorAll(
        IEnumerable<HangfireApplicationOptions> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);

        var results = new List<ApplicationStorageHealthResult>();

        foreach (var application in applications)
        {
            ArgumentNullException.ThrowIfNull(application);

            results.Add(MonitorOne(application));
        }

        return results;
    }

    /// <summary>
    /// Evaluates storage health for a single configured application using the same
    /// metric readers and error isolation as <see cref="MonitorAll"/>.
    /// </summary>
    public ApplicationStorageHealthResult MonitorOne(HangfireApplicationOptions application)
    {
        ArgumentNullException.ThrowIfNull(application);

        SqlServerStorage storage;
        try
        {
            storage = _createStorage(application);
        }
        catch (DbException)
        {
            return _appRules.Unavailable(application.Name);
        }

        var schema = ReadSchema(storage);
        var dataFiles = ReadDataFiles(storage);
        var logSpace = ReadLogSpace(storage);
        var logReuseWait = ReadLogReuseWait(storage);
        var activeTransactions = ReadActiveTransactions(storage);
        var serverCount = ReadServerCount(storage);

        return _appRules.FromMetrics(
            application.Name,
            schema,
            dataFiles,
            logSpace,
            logReuseWait,
            activeTransactions,
            serverCount);
    }

    private SchemaVersionHealthResult ReadSchema(SqlServerStorage storage)
    {
        try
        {
            var version = _getSchemaVersion(storage);
            return _schemaRules.Evaluate(
                version,
                SchemaVersionHealthRules.DefaultExpectedSchemaVersion);
        }
        catch (DbException)
        {
            return _schemaRules.Unavailable(SchemaVersionHealthRules.DefaultExpectedSchemaVersion);
        }
    }

    private DataFileSpaceHealthResult ReadDataFiles(SqlServerStorage storage)
    {
        try
        {
            var metrics = _getDataFileSpace(storage);
            return _dataFileRules.Evaluate(metrics);
        }
        catch (DbException)
        {
            return _dataFileRules.Unavailable();
        }
    }

    private LogSpaceMetrics? ReadLogSpace(SqlServerStorage storage)
    {
        try
        {
            return _getLogSpace(storage);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private LogReuseWaitMetrics? ReadLogReuseWait(SqlServerStorage storage)
    {
        try
        {
            return _getLogReuseWait(storage);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private ActiveTransactionMetrics? ReadActiveTransactions(SqlServerStorage storage)
    {
        try
        {
            return _getActiveTransactions(storage);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private long ReadServerCount(SqlServerStorage storage)
    {
        try
        {
            return _getServerCount(storage);
        }
        catch (DbException)
        {
            return 0;
        }
    }
}
