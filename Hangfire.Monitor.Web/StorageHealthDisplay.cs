using System.Globalization;
using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Web;

/// <summary>
/// Formats Storage Health domain results for the Storage Health pages.
/// Presentation only — does not evaluate health or acquire metrics.
/// </summary>
public static class StorageHealthDisplay
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Returns <c>actual / expected</c>, or <c>-</c> when the actual version is unavailable.
    /// </summary>
    public static string FormatSchema(SchemaVersionHealthResult schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (schema.ActualVersion is null)
        {
            return "-";
        }

        return string.Create(
            Invariant,
            $"{schema.ActualVersion.Value} / {schema.ExpectedVersion}");
    }

    /// <summary>
    /// Returns the CSS classes for the Schema badge.
    /// Uses <see cref="SchemaVersionHealthResult.Status"/> already computed by domain rules —
    /// does not re-evaluate version comparison.
    /// </summary>
    public static string FormatSchemaBadgeCssClass(SchemaVersionHealthResult schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        return string.Create(
            Invariant,
            $"status-badge status-{schema.Status.ToString().ToLowerInvariant()}");
    }

    /// <summary>
    /// Returns data-file used megabytes, or <c>-</c> when unavailable.
    /// Availability follows <see cref="DataFileSpaceHealthResult.UsedPercent"/> (null = unavailable).
    /// </summary>
    public static string FormatDataFileUsed(DataFileSpaceHealthResult dataFiles, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(dataFiles);
        ArgumentNullException.ThrowIfNull(culture);

        if (dataFiles.UsedPercent is null)
        {
            return "-";
        }

        return FormatMegabytes(dataFiles.UsedMB, culture);
    }

    /// <summary>
    /// Returns used percent with two decimal places, or <c>-</c> when unavailable.
    /// </summary>
    public static string FormatDataFileSpace(DataFileSpaceHealthResult dataFiles, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(dataFiles);
        ArgumentNullException.ThrowIfNull(culture);

        if (dataFiles.UsedPercent is null)
        {
            return "-";
        }

        return FormatPercent(dataFiles.UsedPercent.Value, culture);
    }

    /// <summary>
    /// Returns log used megabytes, or <c>-</c> when not acquired.
    /// </summary>
    public static string FormatLogUsed(LogSpaceMetrics? logSpace, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (logSpace is null)
        {
            return "-";
        }

        return FormatMegabytes(logSpace.UsedLogMB, culture);
    }

    /// <summary>
    /// Returns log used percent with two decimal places, or <c>-</c> when not acquired.
    /// </summary>
    public static string FormatLogSpace(LogSpaceMetrics? logSpace, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (logSpace is null)
        {
            return "-";
        }

        return FormatPercent(logSpace.UsedPercent, culture);
    }

    /// <summary>
    /// Returns the raw log reuse wait description, or <c>-</c> when not acquired.
    /// </summary>
    public static string FormatLogReuse(LogReuseWaitMetrics? logReuseWait)
    {
        if (logReuseWait is null)
        {
            return "-";
        }

        return logReuseWait.WaitDescription;
    }

    /// <summary>
    /// Returns a compact active-transaction summary, or <c>-</c> when not acquired.
    /// </summary>
    public static string FormatTransactions(ActiveTransactionMetrics? transactions)
    {
        if (transactions is null)
        {
            return "-";
        }

        if (transactions.Count == 0)
        {
            return "0";
        }

        var duration = transactions.OldestDurationSeconds ?? 0;
        return string.Create(Invariant, $"{transactions.Count} (oldest: {duration}s)");
    }

    /// <summary>
    /// Returns megabytes with grouping separators, two decimal places, and an <c>MB</c> suffix.
    /// </summary>
    public static string FormatMegabytes(decimal megabytes, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return megabytes.ToString("#,0.00", culture) + " MB";
    }

    /// <summary>
    /// Returns used percent with two decimal places for the detail page, or <c>-</c> when null.
    /// </summary>
    public static string FormatUsedPercent(decimal? percent, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (percent is null)
        {
            return "-";
        }

        return percent.Value.ToString("0.00", culture) + " %";
    }

    /// <summary>
    /// Returns local-time display text for a UTC begin time, or <c>-</c> when null.
    /// Uses the same <c>dd/MM/yyyy HH:mm:ss</c> convention as Failed Jobs.
    /// </summary>
    public static string FormatOldestBeginTime(DateTime? oldestBeginTimeUtc)
    {
        if (oldestBeginTimeUtc is null)
        {
            return "-";
        }

        return AsUtc(oldestBeginTimeUtc.Value)
            .ToLocalTime()
            .ToString("dd/MM/yyyy HH:mm:ss", Invariant);
    }

    /// <summary>
    /// Returns a human-readable duration (<c>hh:mm:ss</c>), or <c>-</c> when null.
    /// Zero seconds is a valid duration and formats as <c>00:00:00</c>.
    /// The underlying seconds value remains on the domain model.
    /// </summary>
    public static string FormatOldestDuration(int? oldestDurationSeconds)
    {
        // Only null means unavailable — do not treat 0 as missing.
        if (oldestDurationSeconds is null)
        {
            return "-";
        }

        var duration = TimeSpan.FromSeconds(oldestDurationSeconds.Value);
        return string.Create(
            Invariant,
            $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}");
    }

    /// <summary>
    /// Formats Active Transactions detail fields for the Storage Health detail page.
    /// Oldest begin/duration are based solely on nullability — not on <see cref="ActiveTransactionMetrics.Count"/>.
    /// </summary>
    public static (string Count, string OldestBeginTime, string OldestDuration) FormatActiveTransactionDetails(
        ActiveTransactionMetrics transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        return (
            transactions.Count.ToString(Invariant),
            FormatOldestBeginTime(transactions.OldestBeginTimeUtc),
            FormatOldestDuration(transactions.OldestDurationSeconds));
    }

    /// <summary>
    /// Returns capacity Health display text: Healthy / Warning / Unavailable.
    /// </summary>
    public static string FormatHealth(StorageHealthStatus status) =>
        status switch
        {
            StorageHealthStatus.OK => "Healthy",
            StorageHealthStatus.WARNING => "Warning",
            StorageHealthStatus.UNAVAILABLE => "Unavailable",
            StorageHealthStatus.CRITICAL => "Warning",
            _ => status.ToString()
        };

    /// <summary>
    /// Returns CSS classes for the capacity Health badge.
    /// </summary>
    public static string FormatHealthBadgeCssClass(StorageHealthStatus status)
    {
        var cssStatus = status switch
        {
            StorageHealthStatus.OK => "ok",
            StorageHealthStatus.WARNING => "warning",
            StorageHealthStatus.CRITICAL => "warning",
            StorageHealthStatus.UNAVAILABLE => "unavailable",
            _ => "unavailable"
        };

        return string.Create(Invariant, $"status-badge status-{cssStatus}");
    }

    /// <summary>
    /// Returns a numeric sort key for Health (Warning highest among evaluable states).
    /// </summary>
    public static string FormatHealthSortValue(StorageHealthStatus status) =>
        status switch
        {
            StorageHealthStatus.WARNING => "2",
            StorageHealthStatus.CRITICAL => "2",
            StorageHealthStatus.OK => "1",
            StorageHealthStatus.UNAVAILABLE => "0",
            _ => "0"
        };

    /// <summary>
    /// Formats MaxSize for display (UNLIMITED / NO_GROWTH / numeric MB).
    /// </summary>
    public static string FormatMaxSize(DataFileHeadroomFileMetrics file, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(culture);

        return file.MaxSizeKind switch
        {
            DataFileMaxSizeKind.Unlimited => "UNLIMITED",
            DataFileMaxSizeKind.NoGrowth => "NO_GROWTH",
            DataFileMaxSizeKind.Limited when file.MaxSizeMB is not null =>
                FormatMegabytes(file.MaxSizeMB.Value, culture),
            _ => "-"
        };
    }

    /// <summary>
    /// Formats autogrowth for display.
    /// </summary>
    public static string FormatGrowth(DataFileHeadroomFileMetrics file, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(culture);

        if (file.IsPercentGrowth)
        {
            if (file.GrowthPercent is null)
            {
                return "-";
            }

            return file.GrowthPercent.Value.ToString("0.00", Invariant) + " %";
        }

        if (file.GrowthMB is null)
        {
            return "-";
        }

        return FormatMegabytes(file.GrowthMB.Value, culture);
    }

    /// <summary>
    /// Formats gigabytes with two decimal places and a suffix, or <c>-</c> when null.
    /// </summary>
    public static string FormatGigabytes(decimal? gigabytes, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (gigabytes is null)
        {
            return "-";
        }

        return gigabytes.Value.ToString("#,0.00", culture) + " GB";
    }

    /// <summary>
    /// Returns the display text for <paramref name="status"/>.
    /// </summary>
    public static string FormatStatus(StorageHealthStatus status) =>
        status switch
        {
            StorageHealthStatus.OK => "OK",
            StorageHealthStatus.WARNING => "WARNING",
            StorageHealthStatus.CRITICAL => "CRITICAL",
            StorageHealthStatus.UNAVAILABLE => "UNAVAILABLE",
            _ => status.ToString()
        };

    /// <summary>
    /// Returns a numeric sort key for client table sorting (CRITICAL highest).
    /// </summary>
    public static string FormatStatusSortValue(StorageHealthStatus status) =>
        status switch
        {
            StorageHealthStatus.CRITICAL => "3",
            StorageHealthStatus.WARNING => "2",
            StorageHealthStatus.OK => "1",
            StorageHealthStatus.UNAVAILABLE => "0",
            _ => "0"
        };

    /// <summary>
    /// Returns the shared status-table row CSS class when Hangfire has no registered servers,
    /// or <c>null</c> when the row should render normally.
    /// </summary>
    public static string? FormatRowCssClass(long serverCount) =>
        serverCount == 0 ? "status-row-no-servers" : null;

    private static string FormatPercent(decimal percent, CultureInfo culture) =>
        percent.ToString("0.00", culture) + "%";

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
