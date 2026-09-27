namespace Hangfire.Monitor.Domain;

/// <summary>
/// Pure rules that evaluate real growth capacity from per-file headroom metrics.
/// Does not use used-percent thresholds. Volume stats missing is never a failure.
/// </summary>
public sealed class DataFileHeadroomHealthRules
{
    /// <summary>
    /// Evaluates ROWS file headroom. Warning only when a file has no free space
    /// inside the file and cannot grow further because of MaxSize/NO_GROWTH.
    /// </summary>
    public DataFileHeadroomHealthResult Evaluate(IReadOnlyList<DataFileHeadroomFileMetrics> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        foreach (var file in files)
        {
            if (IsCapacityWarning(file))
            {
                return new DataFileHeadroomHealthResult(
                    StorageHealthStatus.WARNING,
                    Diagnosis:
                    $"Data file '{file.FileName}' has no free space inside the file and cannot grow further because of its configured maximum size.",
                    Recommendation:
                    "A DBA should raise MAXSIZE, grow the file, or free space — see the resolution guide. Hangfire Monitor does not apply changes.",
                    TriggeringFile: file,
                    Files: files);
            }
        }

        return new DataFileHeadroomHealthResult(
            StorageHealthStatus.OK,
            Diagnosis: "No storage capacity issue detected from the available metrics.",
            Recommendation: "No action required.",
            TriggeringFile: null,
            Files: files);
    }

    /// <summary>
    /// True when the file has exhausted internal free space and growth is blocked by MaxSize.
    /// </summary>
    public static bool IsCapacityWarning(DataFileHeadroomFileMetrics file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.FreeMB > 0m)
        {
            return false;
        }

        return file.MaxSizeKind switch
        {
            DataFileMaxSizeKind.NoGrowth => true,
            DataFileMaxSizeKind.Limited =>
                file.MaxSizeMB is not null && file.CurrentSizeMB >= file.MaxSizeMB.Value,
            DataFileMaxSizeKind.Unlimited => false,
            _ => false
        };
    }
}
