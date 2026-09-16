using Hangfire;
using Hangfire.SqlServer;
using Hangfire.Storage.Monitoring;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Reads Hangfire monitoring statistics for an existing storage via the public Monitoring API.
/// Prefer <see cref="GetStatistics"/> when both Failed and Servers are needed so
/// <c>GetStatistics()</c> runs once.
/// </summary>
public class FailedJobCountReader
{
    /// <summary>
    /// Returns the full <see cref="StatisticsDto"/> for <paramref name="storage"/>.
    /// Does not set <see cref="JobStorage.Current"/> or create a new storage instance.
    /// </summary>
    public StatisticsDto GetStatistics(SqlServerStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        return GetStatistics((JobStorage)storage);
    }

    /// <summary>
    /// Shared implementation over <see cref="JobStorage"/> so unit tests can supply a stub storage.
    /// </summary>
    internal StatisticsDto GetStatistics(JobStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        return storage.GetMonitoringApi().GetStatistics();
    }

    /// <summary>
    /// Returns the number of jobs currently in the Failed state for <paramref name="storage"/>.
    /// Uses <c>GetStatistics().Failed</c> (uncapped) rather than <c>FailedCount()</c>.
    /// </summary>
    public long GetFailedCount(SqlServerStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        return GetFailedCount((JobStorage)storage);
    }

    /// <summary>
    /// Shared implementation over <see cref="JobStorage"/> so unit tests can supply a stub storage.
    /// </summary>
    internal long GetFailedCount(JobStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        return GetStatistics(storage).Failed;
    }
}
