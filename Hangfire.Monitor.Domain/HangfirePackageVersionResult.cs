namespace Hangfire.Monitor.Domain;

/// <summary>
/// Domain outcome of comparing an application's Hangfire package version
/// against the configured latest version.
/// Construct via <see cref="HangfirePackageVersionRules"/>.
/// </summary>
public sealed record HangfirePackageVersionResult(
    string? ActualVersion,
    string? ExpectedVersion,
    StorageHealthStatus Status);
