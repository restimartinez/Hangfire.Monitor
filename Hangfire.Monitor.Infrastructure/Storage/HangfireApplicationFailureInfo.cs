namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Failed-job and server metrics for a single Hangfire application (Infrastructure read model).
/// Not the domain monitoring status model (<c>OK</c>/<c>FAILED</c>/<c>UNAVAILABLE</c>).
/// </summary>
public sealed record HangfireApplicationFailureInfo(
    long FailedCount,
    DateTime? LastFailedAt,
    long ServerCount);
