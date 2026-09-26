namespace Hangfire.Monitor.Domain;

/// <summary>
/// Raw Storage Health observation for one configured application.
/// Groups existing metric payloads for the detail page; does not evaluate thresholds.
/// </summary>
public sealed record StorageHealthDetails(
    string ApplicationName,
    long ServerCount,
    DataFileSpaceHealthResult DataFiles,
    LogSpaceMetrics? Log,
    LogReuseWaitMetrics? LogReuse,
    ActiveTransactionMetrics? ActiveTransactions,
    StorageHealthStatus Status)
{
    /// <summary>
    /// True when the application could not be queried as a whole.
    /// </summary>
    public bool IsUnavailable => Status == StorageHealthStatus.UNAVAILABLE;

    /// <summary>
    /// Maps an existing application monitoring result into the detail observation model.
    /// </summary>
    public static StorageHealthDetails From(ApplicationStorageHealthResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new StorageHealthDetails(
            result.ApplicationName,
            result.ServerCount,
            result.DataFiles,
            result.LogSpace,
            result.LogReuseWait,
            result.ActiveTransactions,
            result.Status);
    }
}
