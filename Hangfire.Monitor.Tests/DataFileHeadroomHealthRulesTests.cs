using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class DataFileHeadroomHealthRulesTests
{
    private readonly DataFileHeadroomHealthRules _rules = new();

    [Fact]
    public void Evaluate_WhenUnlimitedAndHighUsed_ReturnsOk()
    {
        var files = new[]
        {
            File(
                freeMb: 2m,
                usedPercent: 98m,
                maxSizeKind: DataFileMaxSizeKind.Unlimited,
                maxSizeMb: null,
                currentSizeMb: 1000m)
        };

        var result = _rules.Evaluate(files);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Null(result.TriggeringFile);
        Assert.Contains("No storage capacity issue", result.Diagnosis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WhenVolumeStatsMissing_ReturnsOk_NotCritical()
    {
        var files = new[]
        {
            File(
                freeMb: 50m,
                usedPercent: 50m,
                maxSizeKind: DataFileMaxSizeKind.Unlimited,
                maxSizeMb: null,
                currentSizeMb: 100m,
                volumeTotalGb: null,
                volumeFreeGb: null,
                volumeFreePercent: null,
                volumeMountPoint: null)
        };

        var result = _rules.Evaluate(files);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Null(result.TriggeringFile);
    }

    [Fact]
    public void Evaluate_WhenAtMaxSizeAndNoFreeSpace_ReturnsWarning()
    {
        var files = new[]
        {
            File(
                freeMb: 0m,
                usedPercent: 100m,
                maxSizeKind: DataFileMaxSizeKind.Limited,
                maxSizeMb: 500m,
                currentSizeMb: 500m,
                fileName: "HangfireData")
        };

        var result = _rules.Evaluate(files);

        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
        Assert.NotNull(result.TriggeringFile);
        Assert.Equal("HangfireData", result.TriggeringFile!.FileName);
        Assert.Contains("cannot grow", result.Diagnosis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WhenAtMaxSizeButHasFreeSpace_ReturnsOk()
    {
        var files = new[]
        {
            File(
                freeMb: 20m,
                usedPercent: 80m,
                maxSizeKind: DataFileMaxSizeKind.Limited,
                maxSizeMb: 500m,
                currentSizeMb: 500m)
        };

        var result = _rules.Evaluate(files);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void Evaluate_WhenNoGrowthAndNoFreeSpace_ReturnsWarning()
    {
        var files = new[]
        {
            File(
                freeMb: 0m,
                usedPercent: 100m,
                maxSizeKind: DataFileMaxSizeKind.NoGrowth,
                maxSizeMb: null,
                currentSizeMb: 200m)
        };

        var result = _rules.Evaluate(files);

        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
    }

    [Fact]
    public void Evaluate_WhenNoGrowthButHasFreeSpace_ReturnsOk()
    {
        var files = new[]
        {
            File(
                freeMb: 10m,
                usedPercent: 90m,
                maxSizeKind: DataFileMaxSizeKind.NoGrowth,
                maxSizeMb: null,
                currentSizeMb: 200m)
        };

        var result = _rules.Evaluate(files);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void Evaluate_WhenUnlimitedAndZeroFree_ReturnsOk()
    {
        // Can still grow until the volume is full — not a MaxSize ceiling Warning.
        var files = new[]
        {
            File(
                freeMb: 0m,
                usedPercent: 100m,
                maxSizeKind: DataFileMaxSizeKind.Unlimited,
                maxSizeMb: null,
                currentSizeMb: 1000m)
        };

        var result = _rules.Evaluate(files);

        Assert.Equal(StorageHealthStatus.OK, result.Status);
    }

    [Fact]
    public void Evaluate_WhenFilesIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _rules.Evaluate(null!));
    }

    [Fact]
    public void InterpretUnlimited_NeverInventNumericMaxSize()
    {
        var file = File(
            freeMb: 1m,
            usedPercent: 50m,
            maxSizeKind: DataFileMaxSizeKind.Unlimited,
            maxSizeMb: null,
            currentSizeMb: 100m);

        Assert.Equal(DataFileMaxSizeKind.Unlimited, file.MaxSizeKind);
        Assert.Null(file.MaxSizeMB);
    }

    private static DataFileHeadroomFileMetrics File(
        decimal freeMb,
        decimal usedPercent,
        DataFileMaxSizeKind maxSizeKind,
        decimal? maxSizeMb,
        decimal currentSizeMb,
        string fileName = "data",
        decimal? volumeTotalGb = 80m,
        decimal? volumeFreeGb = 40m,
        decimal? volumeFreePercent = 50m,
        string? volumeMountPoint = "C:\\") =>
        new(
            FileId: 1,
            FileName: fileName,
            CurrentSizeMB: currentSizeMb,
            UsedMB: currentSizeMb - freeMb,
            FreeMB: freeMb,
            UsedPercent: usedPercent,
            MaxSizeKind: maxSizeKind,
            MaxSizeMB: maxSizeMb,
            GrowthMB: 64m,
            GrowthPercent: null,
            IsPercentGrowth: false,
            VolumeMountPoint: volumeMountPoint,
            VolumeTotalGB: volumeTotalGb,
            VolumeFreeGB: volumeFreeGb,
            VolumeFreePercent: volumeFreePercent);
}
