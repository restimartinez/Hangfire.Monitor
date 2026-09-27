namespace Hangfire.Monitor.Domain;

/// <summary>
/// Per-file data-file headroom metrics (ROWS files) for Hangfire storage.
/// Acquisition model only — no health status. Volume fields may be null when
/// <c>sys.dm_os_volume_stats</c> is unavailable.
/// </summary>
public sealed record DataFileHeadroomFileMetrics(
    int FileId,
    string FileName,
    decimal CurrentSizeMB,
    decimal UsedMB,
    decimal FreeMB,
    decimal? UsedPercent,
    DataFileMaxSizeKind MaxSizeKind,
    decimal? MaxSizeMB,
    decimal? GrowthMB,
    int? GrowthPercent,
    bool IsPercentGrowth,
    string? VolumeMountPoint,
    decimal? VolumeTotalGB,
    decimal? VolumeFreeGB,
    decimal? VolumeFreePercent);
