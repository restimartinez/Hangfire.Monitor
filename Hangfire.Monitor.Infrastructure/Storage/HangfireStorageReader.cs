using Hangfire;
using Hangfire.Monitor.Domain;
using Hangfire.SqlServer;
using Hangfire.Storage.Monitoring;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Reads failed-job count, registered server count, and latest failure timestamp for one
/// configured Hangfire application, using a single <see cref="SqlServerStorage"/> instance.
/// Failed and Servers come from one <see cref="StatisticsDto"/> (<c>GetStatistics()</c> once).
/// </summary>
public class HangfireStorageReader
{
    private readonly Func<HangfireApplicationOptions, SqlServerStorage> _createStorage;
    private readonly Func<SqlServerStorage, StatisticsDto> _getStatistics;
    private readonly Func<SqlServerStorage, DateTime?> _getLastFailedAt;

    public HangfireStorageReader(SqlServerStorageFactory storageFactory)
        : this(
            storageFactory,
            new FailedJobCountReader(),
            new LastFailedAtReader())
    {
    }

    public HangfireStorageReader(
        SqlServerStorageFactory storageFactory,
        FailedJobCountReader failedJobCountReader,
        LastFailedAtReader lastFailedAtReader)
    {
        ArgumentNullException.ThrowIfNull(storageFactory);
        ArgumentNullException.ThrowIfNull(failedJobCountReader);
        ArgumentNullException.ThrowIfNull(lastFailedAtReader);

        _createStorage = storageFactory.Create;
        _getStatistics = failedJobCountReader.GetStatistics;
        _getLastFailedAt = lastFailedAtReader.GetLastFailedAt;
    }

    /// <summary>
    /// Test seam: substitute storage creation and the two reads without a live SQL Server.
    /// </summary>
    internal HangfireStorageReader(
        Func<HangfireApplicationOptions, SqlServerStorage> createStorage,
        Func<SqlServerStorage, StatisticsDto> getStatistics,
        Func<SqlServerStorage, DateTime?> getLastFailedAt)
    {
        _createStorage = createStorage ?? throw new ArgumentNullException(nameof(createStorage));
        _getStatistics = getStatistics ?? throw new ArgumentNullException(nameof(getStatistics));
        _getLastFailedAt = getLastFailedAt ?? throw new ArgumentNullException(nameof(getLastFailedAt));
    }

    /// <summary>
    /// Creates one <see cref="SqlServerStorage"/> for <paramref name="application"/> and reads
    /// <see cref="HangfireApplicationFailureInfo.FailedCount"/>,
    /// <see cref="HangfireApplicationFailureInfo.ServerCount"/> (from one <see cref="StatisticsDto"/>),
    /// and <see cref="HangfireApplicationFailureInfo.LastFailedAt"/> from that same instance.
    /// Does not set <see cref="JobStorage.Current"/>. Exceptions from the underlying readers propagate.
    /// </summary>
    public HangfireApplicationFailureInfo Read(HangfireApplicationOptions application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var storage = _createStorage(application);
        var statistics = _getStatistics(storage);
        var lastFailedAt = _getLastFailedAt(storage);

        return new HangfireApplicationFailureInfo(
            statistics.Failed,
            lastFailedAt,
            statistics.Servers);
    }
}
