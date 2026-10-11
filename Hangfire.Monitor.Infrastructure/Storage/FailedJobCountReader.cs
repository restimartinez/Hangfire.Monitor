
using Hangfire.SqlServer;
using Hangfire.Storage.Monitoring;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Reads Hangfire monitoring statistics for an existing storage via the public Monitoring API.
/// Callers that need both Failed and Servers should use one <see cref="GetStatistics"/> call
/// so <c>GetStatistics()</c> runs once.
/// </summary>
public class FailedJobCountReader
{
    /// <summary>
    /// Returns the full <see cref="StatisticsDto"/> for <paramref name="storage"/>.
    /// Does not set <see cref="JobStorage.Current"/> or create a new storage instance.
    /// Failed counts come from <see cref="StatisticsDto.Failed"/> (uncapped), not <c>FailedCount()</c>.
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
}
