namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Failed-job metrics for a single Hangfire application (Infrastructure read model for HM-032).
/// Not the domain monitoring status model (<c>OK</c>/<c>FAILED</c>/<c>UNAVAILABLE</c>).
/// </summary>
public sealed record HangfireApplicationFailureInfo(long FailedCount, DateTime? LastFailedAt);
