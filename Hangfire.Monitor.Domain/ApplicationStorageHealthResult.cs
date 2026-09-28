namespace Hangfire.Monitor.Domain;

/// <summary>
/// Domain outcome of evaluating storage health for one Hangfire application.
/// Construct via <see cref="ApplicationStorageHealthRules"/>.
/// <see cref="Status"/> is capacity Health only (Schema is separate).
/// </summary>
public sealed record ApplicationStorageHealthResult(
    string ApplicationName,
    StorageHealthStatus Status,
    SchemaVersionHealthResult Schema,
    DataFileSpaceHealthResult DataFiles,
    IReadOnlyList<DataFileHeadroomFileMetrics>? DataFileHeadroom,
    LogSpaceMetrics? LogSpace,
    LogReuseWaitMetrics? LogReuseWait,
    ActiveTransactionMetrics? ActiveTransactions,
    long ServerCount,
    string Diagnosis,
    string? FailureReason,
    StorageResolutionGuide? Resolution,
    string Version = "");
