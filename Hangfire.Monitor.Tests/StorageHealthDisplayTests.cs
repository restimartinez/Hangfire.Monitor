using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Web;

namespace Hangfire.Monitor.Tests;

public class StorageHealthDisplayTests
{
    [Fact]
    public void FormatSchema_WhenVersionsMatch_ReturnsActualSlashExpected()
    {
        var schema = new SchemaVersionHealthResult(
            ActualVersion: 9,
            ExpectedVersion: 9,
            StorageHealthStatus.OK,
            Diagnosis: "ok",
            Recommendation: "none");

        Assert.Equal("9 / 9", StorageHealthDisplay.FormatSchema(schema));
    }

    [Fact]
    public void FormatSchema_WhenActualIsLower_ReturnsActualSlashExpected()
    {
        var schema = new SchemaVersionHealthResult(
            ActualVersion: 8,
            ExpectedVersion: 9,
            StorageHealthStatus.WARNING,
            Diagnosis: "lower",
            Recommendation: "review");

        Assert.Equal("8 / 9", StorageHealthDisplay.FormatSchema(schema));
    }

    [Fact]
    public void FormatSchema_WhenActualIsHigher_ReturnsActualSlashExpected()
    {
        var schema = new SchemaVersionHealthResult(
            ActualVersion: 10,
            ExpectedVersion: 9,
            StorageHealthStatus.WARNING,
            Diagnosis: "higher",
            Recommendation: "review");

        Assert.Equal("10 / 9", StorageHealthDisplay.FormatSchema(schema));
    }

    [Fact]
    public void FormatSchema_WhenUnavailable_ReturnsDash()
    {
        var schema = new SchemaVersionHealthResult(
            ActualVersion: null,
            ExpectedVersion: 9,
            StorageHealthStatus.UNAVAILABLE,
            Diagnosis: "missing",
            Recommendation: "verify");

        Assert.Equal("-", StorageHealthDisplay.FormatSchema(schema));
    }

    [Fact]
    public void FormatDataFileSpace_WhenUsedPercentPresent_ReturnsTwoDecimalPercent()
    {
        var dataFiles = new DataFileSpaceHealthResult(
            AllocatedMB: 100m,
            UsedMB: 82.5m,
            FreeMB: 17.5m,
            UsedPercent: 82.5m,
            StorageHealthStatus.WARNING,
            Diagnosis: "approaching",
            Recommendation: "review");

        Assert.Equal("82.50%", StorageHealthDisplay.FormatDataFileSpace(dataFiles));
    }

    [Fact]
    public void FormatDataFileSpace_WhenUsedPercentNull_ReturnsDash()
    {
        var dataFiles = new DataFileSpaceHealthResult(
            AllocatedMB: 0m,
            UsedMB: 0m,
            FreeMB: 0m,
            UsedPercent: null,
            StorageHealthStatus.UNAVAILABLE,
            Diagnosis: "missing",
            Recommendation: "verify");

        Assert.Equal("-", StorageHealthDisplay.FormatDataFileSpace(dataFiles));
    }

    [Fact]
    public void FormatLogSpace_WhenPresent_ReturnsTwoDecimalPercent()
    {
        var logSpace = new LogSpaceMetrics(100m, 65.2m, 34.8m, 65.2m);

        Assert.Equal("65.20%", StorageHealthDisplay.FormatLogSpace(logSpace));
    }

    [Fact]
    public void FormatLogSpace_WhenNull_ReturnsDash()
    {
        Assert.Equal("-", StorageHealthDisplay.FormatLogSpace(null));
    }

    [Fact]
    public void FormatLogReuse_WhenPresent_ReturnsWaitDescription()
    {
        var reuse = new LogReuseWaitMetrics(2, "LOG_BACKUP", "FULL");

        Assert.Equal("LOG_BACKUP", StorageHealthDisplay.FormatLogReuse(reuse));
    }

    [Fact]
    public void FormatLogReuse_WhenNull_ReturnsDash()
    {
        Assert.Equal("-", StorageHealthDisplay.FormatLogReuse(null));
    }

    [Fact]
    public void FormatTransactions_WhenActive_ReturnsCountAndOldestDuration()
    {
        var transactions = new ActiveTransactionMetrics(
            Count: 3,
            OldestBeginTimeUtc: new DateTime(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc),
            OldestDurationSeconds: 125);

        Assert.Equal("3 (oldest: 125s)", StorageHealthDisplay.FormatTransactions(transactions));
    }

    [Fact]
    public void FormatTransactions_WhenZero_ReturnsZero()
    {
        var transactions = new ActiveTransactionMetrics(0, null, null);

        Assert.Equal("0", StorageHealthDisplay.FormatTransactions(transactions));
    }

    [Fact]
    public void FormatTransactions_WhenNull_ReturnsDash()
    {
        Assert.Equal("-", StorageHealthDisplay.FormatTransactions(null));
    }

    [Theory]
    [InlineData(StorageHealthStatus.OK, "OK")]
    [InlineData(StorageHealthStatus.WARNING, "WARNING")]
    [InlineData(StorageHealthStatus.CRITICAL, "CRITICAL")]
    [InlineData(StorageHealthStatus.UNAVAILABLE, "UNAVAILABLE")]
    public void FormatStatus_ReturnsExpectedText(StorageHealthStatus status, string expected)
    {
        Assert.Equal(expected, StorageHealthDisplay.FormatStatus(status));
    }

    [Theory]
    [InlineData(StorageHealthStatus.CRITICAL, "3")]
    [InlineData(StorageHealthStatus.WARNING, "2")]
    [InlineData(StorageHealthStatus.OK, "1")]
    [InlineData(StorageHealthStatus.UNAVAILABLE, "0")]
    public void FormatStatusSortValue_ReturnsSeverityOrder(StorageHealthStatus status, string expected)
    {
        Assert.Equal(expected, StorageHealthDisplay.FormatStatusSortValue(status));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void FormatRowCssClass_WhenServersPresent_ReturnsNull(long serverCount)
    {
        Assert.Null(StorageHealthDisplay.FormatRowCssClass(serverCount));
    }

    [Fact]
    public void FormatRowCssClass_WhenServerCountIsZero_ReturnsNoServersClass()
    {
        Assert.Equal("status-row-no-servers", StorageHealthDisplay.FormatRowCssClass(0));
    }

    [Fact]
    public void Formatters_DoNotApplyHealthInterpretation_ToAcquisitionOnlyMetrics()
    {
        // High log usage and long transaction age are still raw presentation text.
        var logSpace = new LogSpaceMetrics(100m, 99m, 1m, 99m);
        var reuse = new LogReuseWaitMetrics(4, "ACTIVE_TRANSACTION", "FULL");
        var transactions = new ActiveTransactionMetrics(
            5,
            new DateTime(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc),
            3600);

        Assert.Equal("99.00%", StorageHealthDisplay.FormatLogSpace(logSpace));
        Assert.Equal("ACTIVE_TRANSACTION", StorageHealthDisplay.FormatLogReuse(reuse));
        Assert.Equal("5 (oldest: 3600s)", StorageHealthDisplay.FormatTransactions(transactions));
    }

    [Fact]
    public void FormatMegabytes_FormatsGroupedMbValues()
    {
        Assert.Equal("12,450 MB", StorageHealthDisplay.FormatMegabytes(12450m));
        Assert.Equal("320.5 MB", StorageHealthDisplay.FormatMegabytes(320.5m));
    }

    [Fact]
    public void FormatUsedPercent_WhenPresent_ReturnsOneDecimalPercent()
    {
        Assert.Equal("58.8 %", StorageHealthDisplay.FormatUsedPercent(58.8m));
        Assert.Equal("15.6 %", StorageHealthDisplay.FormatUsedPercent(15.6m));
    }

    [Fact]
    public void FormatUsedPercent_WhenNull_ReturnsDash()
    {
        Assert.Equal("-", StorageHealthDisplay.FormatUsedPercent(null));
    }

    [Fact]
    public void FormatOldestDuration_WhenPresent_ReturnsHhMmSs()
    {
        Assert.Equal("00:31:42", StorageHealthDisplay.FormatOldestDuration(1902));
        Assert.Equal("01:00:00", StorageHealthDisplay.FormatOldestDuration(3600));
    }

    [Fact]
    public void FormatOldestDuration_WhenZero_ReturnsZeroTime_NotDash()
    {
        Assert.Equal("00:00:00", StorageHealthDisplay.FormatOldestDuration(0));
    }

    [Fact]
    public void FormatOldestDuration_WhenNull_ReturnsDash()
    {
        Assert.Equal("-", StorageHealthDisplay.FormatOldestDuration(null));
    }

    [Fact]
    public void FormatOldestBeginTime_WhenNull_ReturnsDash()
    {
        Assert.Equal("-", StorageHealthDisplay.FormatOldestBeginTime(null));
    }

    [Fact]
    public void FormatOldestBeginTime_WhenPresent_UsesLocalDdMmYyyyHhMmSs()
    {
        var utc = new DateTime(2026, 9, 26, 9, 42, 17, DateTimeKind.Utc);
        var expected = utc.ToLocalTime().ToString(
            "dd/MM/yyyy HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expected, StorageHealthDisplay.FormatOldestBeginTime(utc));
    }

    [Fact]
    public void FormatActiveTransactionDetails_WhenCountIsZero_ReturnsZeroAndDashes()
    {
        var transactions = new ActiveTransactionMetrics(0, null, null);

        var display = StorageHealthDisplay.FormatActiveTransactionDetails(transactions);

        Assert.Equal("0", display.Count);
        Assert.Equal("-", display.OldestBeginTime);
        Assert.Equal("-", display.OldestDuration);
    }

    [Fact]
    public void FormatActiveTransactionDetails_WhenCountIsOne_AndDurationZero_DisplaysAllValues()
    {
        var begin = new DateTime(2026, 9, 26, 12, 38, 17, DateTimeKind.Utc);
        var transactions = new ActiveTransactionMetrics(1, begin, 0);

        var display = StorageHealthDisplay.FormatActiveTransactionDetails(transactions);

        Assert.Equal("1", display.Count);
        Assert.Equal(StorageHealthDisplay.FormatOldestBeginTime(begin), display.OldestBeginTime);
        Assert.Equal("00:00:00", display.OldestDuration);
        Assert.NotEqual("-", display.OldestBeginTime);
    }

    [Fact]
    public void FormatActiveTransactionDetails_WhenCountGreaterThanOne_DisplaysAllValues()
    {
        var begin = new DateTime(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc);
        var transactions = new ActiveTransactionMetrics(3, begin, 1902);

        var display = StorageHealthDisplay.FormatActiveTransactionDetails(transactions);

        Assert.Equal("3", display.Count);
        Assert.Equal(StorageHealthDisplay.FormatOldestBeginTime(begin), display.OldestBeginTime);
        Assert.Equal("00:31:42", display.OldestDuration);
    }

    [Fact]
    public void FormatActiveTransactionDetails_WhenOldestFieldsNull_ReturnsDashes_RegardlessOfCount()
    {
        // Defensive: formatters must not require Count > 1 to show oldest fields;
        // null oldest values always render as '-'.
        var transactions = new ActiveTransactionMetrics(2, null, null);

        var display = StorageHealthDisplay.FormatActiveTransactionDetails(transactions);

        Assert.Equal("2", display.Count);
        Assert.Equal("-", display.OldestBeginTime);
        Assert.Equal("-", display.OldestDuration);
    }
}
