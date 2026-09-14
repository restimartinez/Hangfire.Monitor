using Hangfire;
using Hangfire.Monitor.Domain;
using Hangfire.SqlServer;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Reads failed-job count and latest failure timestamp for one configured Hangfire application,
/// using a single <see cref="SqlServerStorage"/> instance for both operations.
/// </summary>
public class HangfireStorageReader
{
    private readonly Func<HangfireApplicationOptions, SqlServerStorage> _createStorage;
    private readonly Func<SqlServerStorage, long> _getFailedCount;
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
        _getFailedCount = failedJobCountReader.GetFailedCount;
        _getLastFailedAt = lastFailedAtReader.GetLastFailedAt;
    }

    /// <summary>
    /// Test seam: substitute storage creation and the two reads without a live SQL Server.
    /// </summary>
    internal HangfireStorageReader(
        Func<HangfireApplicationOptions, SqlServerStorage> createStorage,
        Func<SqlServerStorage, long> getFailedCount,
        Func<SqlServerStorage, DateTime?> getLastFailedAt)
    {
        _createStorage = createStorage ?? throw new ArgumentNullException(nameof(createStorage));
        _getFailedCount = getFailedCount ?? throw new ArgumentNullException(nameof(getFailedCount));
        _getLastFailedAt = getLastFailedAt ?? throw new ArgumentNullException(nameof(getLastFailedAt));
    }

    /// <summary>
    /// Creates one <see cref="SqlServerStorage"/> for <paramref name="application"/> and reads
    /// <see cref="HangfireApplicationFailureInfo.FailedCount"/> and
    /// <see cref="HangfireApplicationFailureInfo.LastFailedAt"/> from that same instance.
    /// Does not set <see cref="JobStorage.Current"/>. Exceptions from the underlying readers propagate.
    /// </summary>
    public HangfireApplicationFailureInfo Read(HangfireApplicationOptions application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var storage = _createStorage(application);
        var failedCount = _getFailedCount(storage);
        var lastFailedAt = _getLastFailedAt(storage);

        return new HangfireApplicationFailureInfo(failedCount, lastFailedAt);
    }
}
