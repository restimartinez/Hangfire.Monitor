namespace Hangfire.Monitor.Domain;

/// <summary>
/// Pure rules that map <see cref="DataFileSpaceMetrics"/> into
/// <see cref="DataFileSpaceHealthResult"/>. Used-of-allocated percent is
/// informational only — it never produces WARNING or CRITICAL.
/// Callers that need <see cref="StorageHealthStatus.UNAVAILABLE"/> after a
/// <c>DbException</c> must invoke <see cref="Unavailable"/> explicitly.
/// </summary>
public class DataFileSpaceHealthRules
{
    /// <summary>
    /// Maps a successful data-file space read into OK or UNAVAILABLE.
    /// High used percent alone does not indicate capacity risk.
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
