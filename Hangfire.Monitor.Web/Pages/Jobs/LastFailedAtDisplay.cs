using System.Globalization;

namespace Hangfire.Monitor.Web.Pages.Jobs;

/// <summary>
/// Formats Hangfire storage failure timestamps (UTC) for the Failed Jobs table.
/// Sort keys stay on the UTC wall-clock instant; display uses the host local time zone
/// (same presentation approach as the Hangfire Dashboard via moment.js).
/// </summary>
public static class LastFailedAtDisplay
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Returns the HM-081 sort key for <paramref name="utcTimestamp"/>, or empty when null.
    /// Uses the UTC wall-clock components so ordering follows the stored instant.
    /// </summary>
    public static string FormatSortValue(DateTime? utcTimestamp)
    {
        if (!utcTimestamp.HasValue)
        {
            return string.Empty;
        }

        return AsUtc(utcTimestamp.Value)
            .ToString("yyyy-MM-ddTHH:mm:ss", Invariant);
    }

    /// <summary>
    /// Returns local-time display text for <paramref name="utcTimestamp"/>, or <c>-</c> when null.
    /// </summary>
    public static string FormatDisplay(DateTime? utcTimestamp)
    {
        if (!utcTimestamp.HasValue)
        {
            return "-";
        }

        return AsUtc(utcTimestamp.Value)
            .ToLocalTime()
            .ToString("dd/MM/yyyy HH:mm:ss", Invariant);
    }

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
