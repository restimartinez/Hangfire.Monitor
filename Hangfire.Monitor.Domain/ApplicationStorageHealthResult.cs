namespace Hangfire.Monitor.Domain;

/// <summary>
/// Domain outcome of evaluating storage health for one Hangfire application.
/// Construct via <see cref="ApplicationStorageHealthRules"/>.
/// </summary>
public sealed record ApplicationStorageHealthResult(
    string ApplicationName,
    StorageHealthStatus Status,
    SchemaVersionHealthResult Schema,
    DataFileSpaceHealthResult DataFiles,
    LogSpaceMetrics? LogSpace,
    LogReuseWaitMetrics? LogReuseWait,
    ActiveTransactionMetrics? ActiveTransactions,
    long ServerCount);
