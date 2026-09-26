namespace Hangfire.Monitor.Domain;

/// <summary>
/// Pure rules that map <see cref="DataFileSpaceMetrics"/> into
/// <see cref="DataFileSpaceHealthResult"/>. Does not catch storage exceptions;
/// callers that need <see cref="StorageHealthStatus.UNAVAILABLE"/> after a
/// <c>DbException</c> must invoke <see cref="Unavailable"/> explicitly.
/// </summary>
public class DataFileSpaceHealthRules
{
    /// <summary>
    /// Used percent at or above this value (and at or below
    /// <see cref="DefaultCriticalThresholdPercent"/>) maps to WARNING.
    /// </summary>
    public const decimal DefaultWarningThresholdPercent = 80m;

    /// <summary>
    /// Used percent above this value maps to CRITICAL.
    /// </summary>
    public const decimal DefaultCriticalThresholdPercent = 90m;

    /// <summary>
    /// Maps a successful data-file space read into OK, WARNING, CRITICAL, or UNAVAILABLE.
    /// </summary>
    public DataFileSpaceHealthResult Evaluate(DataFileSpaceMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        if (metrics.AllocatedMB < 0m || metrics.UsedMB < 0m || metrics.FreeMB < 0m)
        {
            return Invalid(metrics);
        }

        if (metrics.AllocatedMB == 0m)
        {
            return Invalid(metrics);
        }

        if (metrics.UsedMB > metrics.AllocatedMB)
        {
            return Invalid(metrics);
        }

        var usedPercent = Math.Round(
            metrics.UsedMB / metrics.AllocatedMB * 100m,
            2,
            MidpointRounding.AwayFromZero);

        if (usedPercent < DefaultWarningThresholdPercent)
        {
            return new DataFileSpaceHealthResult(
                metrics.AllocatedMB,
                metrics.UsedMB,
                metrics.FreeMB,
                usedPercent,
                StorageHealthStatus.OK,
                Diagnosis:
                $"Data files are using {usedPercent}% of the allocated database data-file space.",
                Recommendation: "No action required.");
        }

        if (usedPercent <= DefaultCriticalThresholdPercent)
        {
            return new DataFileSpaceHealthResult(
                metrics.AllocatedMB,
                metrics.UsedMB,
                metrics.FreeMB,
                usedPercent,
                StorageHealthStatus.WARNING,
                Diagnosis:
                $"Data files are using {usedPercent}% of the allocated database data-file space and approaching capacity.",
                Recommendation:
                "Review file growth/autogrowth and available capacity within the allocated data files.");
        }

        return new DataFileSpaceHealthResult(
            metrics.AllocatedMB,
            metrics.UsedMB,
            metrics.FreeMB,
            usedPercent,
            StorageHealthStatus.CRITICAL,
            Diagnosis:
            $"Data files are using {usedPercent}% of the allocated database data-file space and are close to exhausting the allocated space.",
            Recommendation:
            "Review/expand data files or free database space before capacity is exhausted.");
    }

    /// <summary>
    /// Builds a <see cref="StorageHealthStatus.UNAVAILABLE"/> result when data-file space
    /// metrics could not be obtained. Does not interpret exceptions.
    /// </summary>
    public DataFileSpaceHealthResult Unavailable()
    {
        return new DataFileSpaceHealthResult(
            AllocatedMB: 0m,
            UsedMB: 0m,
            FreeMB: 0m,
            UsedPercent: null,
            StorageHealthStatus.UNAVAILABLE,
            Diagnosis: "Data file space metrics could not be obtained.",
            Recommendation:
            "Verify data-file space metrics and required permissions on sys.dm_db_file_space_usage.");
    }

    private static DataFileSpaceHealthResult Invalid(DataFileSpaceMetrics metrics)
    {
        return new DataFileSpaceHealthResult(
            metrics.AllocatedMB,
            metrics.UsedMB,
            metrics.FreeMB,
            UsedPercent: null,
            StorageHealthStatus.UNAVAILABLE,
            Diagnosis:
            "Data file space could not be evaluated because the allocated space is zero or the metrics are invalid.",
            Recommendation:
            "Verify data-file space metrics and required permissions.");
    }
}
