using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class DataFileSpaceHealthRulesTests
{
    private readonly DataFileSpaceHealthRules _rules = new();

    [Fact]
    public void Evaluate_WhenUsedPercentIs50_ReturnsOk()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(100m, 50m, 50m));

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Equal(50.00m, result.UsedPercent);
        Assert.Equal(100m, result.AllocatedMB);
        Assert.Equal(50m, result.UsedMB);
        Assert.Equal(50m, result.FreeMB);
        Assert.Equal("No action required.", result.Recommendation);
        AssertContainsAllocatedSpaceClarification(result.Diagnosis);
    }

    [Fact]
    public void Evaluate_WhenUsedPercentIs7999_ReturnsOk()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(10000m, 7999m, 2001m));

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Equal(79.99m, result.UsedPercent);
    }

    [Fact]
    public void Evaluate_WhenUsedPercentIsExactly80_ReturnsWarning()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(100m, 80m, 20m));

        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
        Assert.Equal(80.00m, result.UsedPercent);
        Assert.Contains("approaching capacity", result.Diagnosis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allocated", result.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WhenUsedPercentIs85_ReturnsWarning()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(100m, 85m, 15m));

        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
        Assert.Equal(85.00m, result.UsedPercent);
    }

    [Fact]
    public void Evaluate_WhenUsedPercentIsExactly90_ReturnsWarning()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(100m, 90m, 10m));

        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
        Assert.Equal(90.00m, result.UsedPercent);
    }

    [Fact]
    public void Evaluate_WhenUsedPercentIs9001_ReturnsCritical()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(10000m, 9001m, 999m));

        Assert.Equal(StorageHealthStatus.CRITICAL, result.Status);
        Assert.Equal(90.01m, result.UsedPercent);
        Assert.Contains("exhausting", result.Diagnosis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expand", result.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WhenUsedPercentIs95_ReturnsCritical()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(100m, 95m, 5m));

        Assert.Equal(StorageHealthStatus.CRITICAL, result.Status);
        Assert.Equal(95.00m, result.UsedPercent);
    }

    [Fact]
    public void Evaluate_WhenAllocatedIsZero_ReturnsUnavailable_WithNullUsedPercent()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(0m, 0m, 0m));

        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
        Assert.Null(result.UsedPercent);
        Assert.Equal(0m, result.AllocatedMB);
        AssertContainsUnavailableGuidance(result);
    }

    [Fact]
    public void Evaluate_WhenAllocatedIsNegative_ReturnsUnavailable()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(-1m, 1m, 1m));

        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
        Assert.Null(result.UsedPercent);
    }

    [Fact]
    public void Evaluate_WhenUsedIsNegative_ReturnsUnavailable()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(100m, -1m, 50m));

        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
        Assert.Null(result.UsedPercent);
    }

    [Fact]
    public void Evaluate_WhenFreeIsNegative_ReturnsUnavailable()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(100m, 40m, -1m));

        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
        Assert.Null(result.UsedPercent);
    }

    [Fact]
    public void Evaluate_WhenUsedExceedsAllocated_ReturnsUnavailable()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(100m, 101m, 0m));

        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
        Assert.Null(result.UsedPercent);
    }

    [Fact]
    public void Evaluate_WhenMetricsIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _rules.Evaluate(null!));
    }

    [Fact]
    public void Evaluate_WhenFreeIsInconsistent_DoesNotChangeStatus()
    {
        // FreeMB does not match Allocated - Used; status still follows UsedPercent (50%).
        var result = _rules.Evaluate(new DataFileSpaceMetrics(100m, 50m, 10m));

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Equal(50.00m, result.UsedPercent);
        Assert.Equal(10m, result.FreeMB);
    }

    [Fact]
    public void Unavailable_ReturnsUnavailable_WithNullUsedPercent()
    {
        var result = _rules.Unavailable();

        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
        Assert.Null(result.UsedPercent);
        Assert.Equal(0m, result.AllocatedMB);
        Assert.Equal(0m, result.UsedMB);
        Assert.Equal(0m, result.FreeMB);
        Assert.Contains("could not be obtained", result.Diagnosis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("permission", result.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_RoundsUsedPercent_ToTwoDecimals()
    {
        // 1 / 3 * 100 = 33.333... → 33.33
        var result = _rules.Evaluate(new DataFileSpaceMetrics(3m, 1m, 2m));

        Assert.Equal(33.33m, result.UsedPercent);
        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void Evaluate_Diagnosis_ClarifiesAllocatedDataFiles_NotDisk()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(100m, 50m, 50m));

        AssertContainsAllocatedSpaceClarification(result.Diagnosis);
        Assert.DoesNotContain("disk", result.Diagnosis, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("volume", result.Diagnosis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Recommendations_DifferByStatus()
    {
        var ok = _rules.Evaluate(new DataFileSpaceMetrics(100m, 50m, 50m));
        var warning = _rules.Evaluate(new DataFileSpaceMetrics(100m, 85m, 15m));
        var critical = _rules.Evaluate(new DataFileSpaceMetrics(100m, 95m, 5m));

        Assert.Equal("No action required.", ok.Recommendation);
        Assert.NotEqual(ok.Recommendation, warning.Recommendation);
        Assert.NotEqual(warning.Recommendation, critical.Recommendation);
        Assert.NotEqual(ok.Recommendation, critical.Recommendation);
    }

    [Fact]
    public void DefaultThresholdConstants_AreEightyAndNinety()
    {
        Assert.Equal(80m, DataFileSpaceHealthRules.DefaultWarningThresholdPercent);
        Assert.Equal(90m, DataFileSpaceHealthRules.DefaultCriticalThresholdPercent);
    }

    private static void AssertContainsAllocatedSpaceClarification(string diagnosis)
    {
        Assert.Contains("allocated", diagnosis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data file", diagnosis, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertContainsUnavailableGuidance(DataFileSpaceHealthResult result)
    {
        Assert.Contains("could not be evaluated", result.Diagnosis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("metric", result.Recommendation, StringComparison.OrdinalIgnoreCase);
    }
}
