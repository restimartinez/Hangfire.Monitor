using System.Globalization;
using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Web;

/// <summary>
/// Formats Storage Health domain results for the Storage Health table.
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

    private static string FormatPercent(decimal percent) =>
        percent.ToString("0.00", Invariant) + "%";
}
