namespace Hangfire.Monitor.Domain;

/// <summary>
/// Domain outcome of evaluating data-file headroom capacity.
/// Construct via <see cref="DataFileHeadroomHealthRules"/>.
/// </summary>
public sealed record DataFileHeadroomHealthResult(
    StorageHealthStatus Status,
    string Diagnosis,
    string Recommendation,
    DataFileHeadroomFileMetrics? TriggeringFile,
    IReadOnlyList<DataFileHeadroomFileMetrics> Files);
