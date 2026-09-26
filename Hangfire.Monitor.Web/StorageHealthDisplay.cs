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
    /// Returns used percent with two decimal places, or <c>-</c> when unavailable.
    /// </summary>
    public static string FormatDataFileSpace(DataFileSpaceHealthResult dataFiles)
    {
        ArgumentNullException.ThrowIfNull(dataFiles);

        if (dataFiles.UsedPercent is null)
        {
            return "-";
        }

        return FormatPercent(dataFiles.UsedPercent.Value);
    }

    /// <summary>
    /// Returns log used percent with two decimal places, or <c>-</c> when not acquired.
    /// </summary>
    public static string FormatLogSpace(LogSpaceMetrics? logSpace)
    {
        if (logSpace is null)
        {
            return "-";
        }

        return FormatPercent(logSpace.UsedPercent);
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
    /// Returns megabytes with grouping separators and an <c>MB</c> suffix.
    /// </summary>
    public static string FormatMegabytes(decimal megabytes) =>
        megabytes.ToString("#,0.###", Invariant) + " MB";

    /// <summary>
    /// Returns used percent with one decimal place for the detail page, or <c>-</c> when null.
    /// </summary>
    public static string FormatUsedPercent(decimal? percent)
    {
        if (percent is null)
        {
            return "-";
        }

        return percent.Value.ToString("0.0", Invariant) + " %";
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

    private static string FormatPercent(decimal percent) =>
        percent.ToString("0.00", Invariant) + "%";

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
