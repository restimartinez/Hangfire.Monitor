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
    public void Evaluate_WhenUsedPercentIsExactly80_ReturnsOk_NotWarning()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(100m, 80m, 20m));

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Equal(80.00m, result.UsedPercent);
        Assert.DoesNotContain("approaching capacity", result.Diagnosis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WhenUsedPercentIsExactly90_ReturnsOk_NotWarning()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(100m, 90m, 10m));

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Equal(90.00m, result.UsedPercent);
    }

    [Fact]
    public void Evaluate_WhenUsedPercentIs98_ReturnsOk_NotCritical()
    {
        var result = _rules.Evaluate(new DataFileSpaceMetrics(100m, 98m, 2m));

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Equal(98.00m, result.UsedPercent);
        Assert.DoesNotContain("exhausting", result.Diagnosis, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("No action required.", result.Recommendation);
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
    public void Evaluate_HighUsedPercent_SameRecommendationAsLow()
    {
        var low = _rules.Evaluate(new DataFileSpaceMetrics(100m, 50m, 50m));
        var high = _rules.Evaluate(new DataFileSpaceMetrics(100m, 98m, 2m));

        Assert.Equal(StorageHealthStatus.OK, low.Status);
        Assert.Equal(StorageHealthStatus.OK, high.Status);
        Assert.Equal(low.Recommendation, high.Recommendation);
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
