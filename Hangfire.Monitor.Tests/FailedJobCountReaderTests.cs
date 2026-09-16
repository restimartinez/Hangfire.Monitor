using Hangfire;
using Hangfire.Monitor.Infrastructure.Storage;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

namespace Hangfire.Monitor.Tests;

public class FailedJobCountReaderTests
{
    private readonly FailedJobCountReader _reader = new();

    [Fact]
    public void GetFailedCount_ReturnsFailedFromStatistics()
    {
        var storage = new StubJobStorage(failed: 42, servers: 0);

        var result = _reader.GetFailedCount(storage);

        Assert.Equal(42, result);
    }

    [Fact]
    public void GetFailedCount_ReturnsZero_WhenNoFailedJobs()
    {
        var storage = new StubJobStorage(failed: 0, servers: 0);

        var result = _reader.GetFailedCount(storage);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetStatistics_ReturnsFailedAndServers()
    {
        var storage = new StubJobStorage(failed: 7, servers: 3);

        var result = _reader.GetStatistics(storage);

        Assert.Equal(7, result.Failed);
        Assert.Equal(3, result.Servers);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void GetStatistics_ReturnsServersCount(long servers)
    {
        var storage = new StubJobStorage(failed: 0, servers: servers);

        var result = _reader.GetStatistics(storage);

        Assert.Equal(servers, result.Servers);
    }

    [Fact]
    public void GetFailedCount_PropagatesException_FromMonitoringApi()
    {
        var storage = new StubJobStorage(new InvalidOperationException("storage unavailable"));

        var ex = Assert.Throws<InvalidOperationException>(() => _reader.GetFailedCount(storage));

        Assert.Equal("storage unavailable", ex.Message);
    }

    [Fact]
    public void GetStatistics_PropagatesException_FromMonitoringApi()
    {
        var storage = new StubJobStorage(new InvalidOperationException("storage unavailable"));

        var ex = Assert.Throws<InvalidOperationException>(() => _reader.GetStatistics(storage));

        Assert.Equal("storage unavailable", ex.Message);
    }

    [Fact]
    public void GetFailedCount_DoesNotSetJobStorageCurrent()
    {
        var storage = new StubJobStorage(failed: 1, servers: 0);

        _ = _reader.GetFailedCount(storage);

        Assert.Throws<InvalidOperationException>(() => _ = JobStorage.Current);
    }

    [Fact]
    public void GetStatistics_DoesNotSetJobStorageCurrent()
    {
        var storage = new StubJobStorage(failed: 1, servers: 2);

        _ = _reader.GetStatistics(storage);

        Assert.Throws<InvalidOperationException>(() => _ = JobStorage.Current);
    }

    private sealed class StubJobStorage : JobStorage
    {
        private readonly IMonitoringApi _monitoringApi;

        public StubJobStorage(long failed, long servers)
            : this(new StubMonitoringApi(new StatisticsDto { Failed = failed, Servers = servers }))
        {
        }

        public StubJobStorage(Exception exceptionToThrow)
            : this(new StubMonitoringApi(exceptionToThrow))
        {
        }

        private StubJobStorage(IMonitoringApi monitoringApi)
        {
            _monitoringApi = monitoringApi;
        }

        public override IMonitoringApi GetMonitoringApi() => _monitoringApi;

        public override IStorageConnection GetConnection() =>
            throw new NotSupportedException("Not required for FailedJobCountReader tests.");
    }

    private sealed class StubMonitoringApi : IMonitoringApi
    {
        private readonly StatisticsDto? _statistics;
        private readonly Exception? _exceptionToThrow;

        public StubMonitoringApi(StatisticsDto statistics)
        {
            _statistics = statistics;
        }

        public StubMonitoringApi(Exception exceptionToThrow)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public StatisticsDto GetStatistics()
        {
            if (_exceptionToThrow is not null)
            {
                throw _exceptionToThrow;
            }

            return _statistics!;
        }

        public IList<QueueWithTopEnqueuedJobsDto> Queues() => throw new NotSupportedException();
        public IList<ServerDto> Servers() => throw new NotSupportedException();
        public JobDetailsDto JobDetails(string jobId) => throw new NotSupportedException();
        public JobList<EnqueuedJobDto> EnqueuedJobs(string queue, int from, int perPage) => throw new NotSupportedException();
        public JobList<FetchedJobDto> FetchedJobs(string queue, int from, int perPage) => throw new NotSupportedException();
        public JobList<ProcessingJobDto> ProcessingJobs(int from, int count) => throw new NotSupportedException();
        public JobList<ScheduledJobDto> ScheduledJobs(int from, int count) => throw new NotSupportedException();
        public JobList<SucceededJobDto> SucceededJobs(int from, int count) => throw new NotSupportedException();
        public JobList<FailedJobDto> FailedJobs(int from, int count) => throw new NotSupportedException();
        public JobList<DeletedJobDto> DeletedJobs(int from, int count) => throw new NotSupportedException();
        public long ScheduledCount() => throw new NotSupportedException();
        public long EnqueuedCount(string queue) => throw new NotSupportedException();
        public long FetchedCount(string queue) => throw new NotSupportedException();
        public long FailedCount() => throw new NotSupportedException();
        public long ProcessingCount() => throw new NotSupportedException();
        public long SucceededListCount() => throw new NotSupportedException();
        public long DeletedListCount() => throw new NotSupportedException();
        public IDictionary<DateTime, long> SucceededByDatesCount() => throw new NotSupportedException();
        public IDictionary<DateTime, long> FailedByDatesCount() => throw new NotSupportedException();
        public IDictionary<DateTime, long> HourlySucceededJobs() => throw new NotSupportedException();
        public IDictionary<DateTime, long> HourlyFailedJobs() => throw new NotSupportedException();
    }
}
