namespace Hangfire.Monitor.Domain;

/// <summary>
/// Pure rules that aggregate per-metric storage health into
/// <see cref="ApplicationStorageHealthResult"/>. Only Schema Version and Data File Space
/// participate in the application-level <see cref="StorageHealthStatus"/>.
/// Acquisition-only metrics are preserved and never affect status.
/// </summary>
public sealed class ApplicationStorageHealthRules
{
    private readonly SchemaVersionHealthRules _schemaRules = new();
    private readonly DataFileSpaceHealthRules _dataFileRules = new();

    /// <summary>
    /// Builds an application storage-health result from already-evaluated metric results.
    /// </summary>
    /// <remarks>
    /// Aggregation (Schema + Data Files only):
    /// <list type="bullet">
    /// <item>Ignore <see cref="StorageHealthStatus.UNAVAILABLE"/> when another metric is evaluable</item>
    /// <item>Any <see cref="StorageHealthStatus.CRITICAL"/> → CRITICAL</item>
    /// <item>Else any <see cref="StorageHealthStatus.WARNING"/> → WARNING</item>
    /// <item>Else all known are OK → OK</item>
    /// <item>Both UNAVAILABLE → UNAVAILABLE</item>
    /// <item><c>ServerCount</c> is propagated and does not affect status</item>
    /// </list>
    /// </remarks>
    public ApplicationStorageHealthResult FromMetrics(
        string applicationName,
        SchemaVersionHealthResult schema,
        DataFileSpaceHealthResult dataFiles,
        LogSpaceMetrics? logSpace,
        LogReuseWaitMetrics? logReuseWait,
        ActiveTransactionMetrics? activeTransactions,
        long serverCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(dataFiles);

        if (serverCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(serverCount),
                serverCount,
                "Server count cannot be negative.");
        }

        var status = AggregateStatus(schema.Status, dataFiles.Status);

        return new ApplicationStorageHealthResult(
            applicationName,
            status,
            schema,
            dataFiles,
            logSpace,
            logReuseWait,
            activeTransactions,
            serverCount);
    }

    /// <summary>
    /// Builds a fully unavailable application row when storage health could not be obtained
    /// for the application as a whole.
    /// </summary>
    public ApplicationStorageHealthResult Unavailable(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        return new ApplicationStorageHealthResult(
            applicationName,
            StorageHealthStatus.UNAVAILABLE,
            _schemaRules.Unavailable(SchemaVersionHealthRules.DefaultExpectedSchemaVersion),
            _dataFileRules.Unavailable(),
            LogSpace: null,
            LogReuseWait: null,
            ActiveTransactions: null,
            ServerCount: 0);
    }

    private static StorageHealthStatus AggregateStatus(
        StorageHealthStatus schemaStatus,
        StorageHealthStatus dataFilesStatus)
    {
        var schemaKnown = schemaStatus != StorageHealthStatus.UNAVAILABLE;
        var dataFilesKnown = dataFilesStatus != StorageHealthStatus.UNAVAILABLE;

        if (!schemaKnown && !dataFilesKnown)
        {
            return StorageHealthStatus.UNAVAILABLE;
        }

        if ((schemaKnown && schemaStatus == StorageHealthStatus.CRITICAL)
            || (dataFilesKnown && dataFilesStatus == StorageHealthStatus.CRITICAL))
        {
            return StorageHealthStatus.CRITICAL;
        }

        if ((schemaKnown && schemaStatus == StorageHealthStatus.WARNING)
            || (dataFilesKnown && dataFilesStatus == StorageHealthStatus.WARNING))
        {
            return StorageHealthStatus.WARNING;
        }

        return StorageHealthStatus.OK;
    }
}
