namespace Hangfire.Monitor.Domain;

/// <summary>
/// Pure rules that aggregate storage capacity health into
/// <see cref="ApplicationStorageHealthResult"/>. Schema Version, Log, Log Reuse,
/// Active Transactions, Servers, and data used-% alone never set capacity Status.
/// Only justified headroom Warning or total acquisition failure affect Status.
/// </summary>
public sealed class ApplicationStorageHealthRules
{
    private readonly SchemaVersionHealthRules _schemaRules = new();
    private readonly DataFileSpaceHealthRules _dataFileRules = new();
    private readonly DataFileHeadroomHealthRules _headroomRules = new();

    /// <summary>
    /// Builds an application storage-health result from already-evaluated metric results.
    /// </summary>
    public ApplicationStorageHealthResult FromMetrics(
        string applicationName,
        SchemaVersionHealthResult schema,
        DataFileSpaceHealthResult dataFiles,
        IReadOnlyList<DataFileHeadroomFileMetrics>? dataFileHeadroom,
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

        DataFileHeadroomHealthResult? headroomResult = null;
        if (dataFileHeadroom is not null)
        {
            headroomResult = _headroomRules.Evaluate(dataFileHeadroom);
        }

        var status = headroomResult?.Status ?? StorageHealthStatus.OK;
        var diagnosis = StorageHealthDiagnosisBuilder.BuildDiagnosis(
            status,
            headroomResult,
            failureReason: null);
        var resolution = StorageHealthDiagnosisBuilder.BuildResolution(
            status,
            headroomResult,
            databaseNameHint: null);

        return new ApplicationStorageHealthResult(
            applicationName,
            status,
            schema,
            dataFiles,
            dataFileHeadroom,
            logSpace,
            logReuseWait,
            activeTransactions,
            serverCount,
            diagnosis,
            FailureReason: null,
            resolution);
    }

    /// <summary>
    /// Builds a fully unavailable application row when storage health could not be obtained
    /// for the application as a whole.
    /// </summary>
    public ApplicationStorageHealthResult Unavailable(
        string applicationName,
        string? failureReason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        var status = StorageHealthStatus.UNAVAILABLE;
        var diagnosis = StorageHealthDiagnosisBuilder.BuildDiagnosis(
            status,
            headroom: null,
            failureReason);

        return new ApplicationStorageHealthResult(
            applicationName,
            status,
            _schemaRules.Unavailable(SchemaVersionHealthRules.DefaultExpectedSchemaVersion),
            _dataFileRules.Unavailable(),
            DataFileHeadroom: null,
            LogSpace: null,
            LogReuseWait: null,
            ActiveTransactions: null,
            ServerCount: 0,
            diagnosis,
            failureReason,
            Resolution: null);
    }
}
