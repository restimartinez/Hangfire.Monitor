using System.Globalization;
using Hangfire.Monitor.Web.Pages.Jobs;

namespace Hangfire.Monitor.Tests;

public class LastFailedAtDisplayTests
{
    [Fact]
    public void FormatDisplay_ConvertsUtc_ToLocalTime()
    {
        var utc = new DateTime(2026, 9, 21, 12, 16, 24, DateTimeKind.Utc);
        var expected = utc.ToLocalTime()
            .ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

        var display = LastFailedAtDisplay.FormatDisplay(utc);

        Assert.Equal(expected, display);
    }

    [Fact]
    public void FormatDisplay_TreatsUnspecified_AsUtc_BeforeLocalConversion()
    {
        var unspecified = new DateTime(2026, 9, 21, 12, 16, 24, DateTimeKind.Unspecified);
        var expected = DateTime.SpecifyKind(unspecified, DateTimeKind.Utc)
            .ToLocalTime()
            .ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

        Assert.Equal(expected, LastFailedAtDisplay.FormatDisplay(unspecified));
    }

    [Fact]
    public void FormatDisplay_ReturnsDash_WhenNull()
    {
        Assert.Equal("-", LastFailedAtDisplay.FormatDisplay(null));
    }

    [Fact]
    public void FormatSortValue_ReturnsEmpty_WhenNull()
    {
        Assert.Equal(string.Empty, LastFailedAtDisplay.FormatSortValue(null));
    }

    [Fact]
    public void FormatSortValue_KeepsUtcWallClock_ForHm081Ordering()
    {
        var utc = new DateTime(2026, 9, 21, 12, 16, 24, DateTimeKind.Utc);

        var sortValue = LastFailedAtDisplay.FormatSortValue(utc);

        Assert.Equal("2026-09-21T12:16:24", sortValue);
        Assert.NotEqual(LastFailedAtDisplay.FormatDisplay(utc), sortValue);
    }

    [Fact]
    public void FormatDisplay_UsesEnvironmentOffset_NotFixedHours_ForSummerAndWinter()
    {
        var summerUtc = new DateTime(2026, 9, 21, 12, 16, 24, DateTimeKind.Utc);
        var winterUtc = new DateTime(2026, 1, 15, 12, 16, 24, DateTimeKind.Utc);

        Assert.Equal(
            summerUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
            LastFailedAtDisplay.FormatDisplay(summerUtc));
        Assert.Equal(
            winterUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
            LastFailedAtDisplay.FormatDisplay(winterUtc));

        var summerOffset = TimeZoneInfo.Local.GetUtcOffset(summerUtc);
        var winterOffset = TimeZoneInfo.Local.GetUtcOffset(winterUtc);

        // In time zones that observe DST, a fixed +2h conversion would fail this check.
        if (summerOffset != winterOffset)
        {
            Assert.NotEqual(
                LastFailedAtDisplay.FormatDisplay(summerUtc),
                LastFailedAtDisplay.FormatDisplay(winterUtc));
            Assert.Equal(
                summerUtc.ToLocalTime() - summerUtc,
                summerOffset);
            Assert.Equal(
                winterUtc.ToLocalTime() - winterUtc,
                winterOffset);
        }
    }
}
