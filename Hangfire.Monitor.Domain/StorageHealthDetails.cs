namespace Hangfire.Monitor.Domain;

/// <summary>
/// Storage Health observation for one configured application (detail page).
/// </summary>
public sealed record StorageHealthDetails(
    string ApplicationName,
    long ServerCount,
    DataFileSpaceHealthResult DataFiles,
    IReadOnlyList<DataFileHeadroomFileMetrics>? DataFileHeadroom,
    LogSpaceMetrics? Log,
    LogReuseWaitMetrics? LogReuse,
    ActiveTransactionMetrics? ActiveTransactions,
    StorageHealthStatus Status,
    string Diagnosis,
    string? FailureReason,
    StorageResolutionGuide? Resolution,
    string Version = "")
{
    /// <summary>
    /// True when the application could not be queried as a whole.
    /// </summary>
    public bool IsUnavailable => Status == StorageHealthStatus.UNAVAILABLE;

    /// <summary>
    /// True when capacity Warning requires the Resolution section.
    /// </summary>
    public bool HasResolution => Resolution is not null;

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
            result.DataFileHeadroom,
            result.LogSpace,
            result.LogReuseWait,
            result.ActiveTransactions,
            result.Status,
            result.Diagnosis,
            result.FailureReason,
            result.Resolution,
            result.Version);
    }
}
