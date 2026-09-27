using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Storage;

namespace Hangfire.Monitor.Tests;

public class DataFileHeadroomReaderTests
{
    [Fact]
    public void Build_SelectsRowsFiles_WithVolumeOuterApply()
    {
        var sql = DataFileHeadroomQuery.Build();

        Assert.Contains("sys.database_files", sql, StringComparison.Ordinal);
        Assert.Contains("FILEPROPERTY", sql, StringComparison.Ordinal);
        Assert.Contains("sys.dm_os_volume_stats", sql, StringComparison.Ordinal);
        Assert.Contains("OUTER APPLY", sql, StringComparison.Ordinal);
        Assert.Contains("type_desc = N'ROWS'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER DATABASE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MODIFY FILE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InterpretMaxSize_WhenNegativeOne_ReturnsUnlimitedWithoutInventedMb()
    {
        var (kind, maxSizeMb) = DataFileHeadroomQuery.InterpretMaxSize(-1);

        Assert.Equal(DataFileMaxSizeKind.Unlimited, kind);
        Assert.Null(maxSizeMb);
    }

    [Fact]
    public void InterpretMaxSize_WhenZero_ReturnsNoGrowth()
    {
        var (kind, maxSizeMb) = DataFileHeadroomQuery.InterpretMaxSize(0);

        Assert.Equal(DataFileMaxSizeKind.NoGrowth, kind);
        Assert.Null(maxSizeMb);
    }

    [Fact]
    public void InterpretMaxSize_WhenUnlimitedSentinel_ReturnsUnlimited()
    {
        var (kind, maxSizeMb) = DataFileHeadroomQuery.InterpretMaxSize(268435456L);

        Assert.Equal(DataFileMaxSizeKind.Unlimited, kind);
        Assert.Null(maxSizeMb);
    }

    [Fact]
    public void InterpretMaxSize_WhenFinitePages_ReturnsLimitedMb()
    {
        // 1280 pages = 10 MB
        var (kind, maxSizeMb) = DataFileHeadroomQuery.InterpretMaxSize(1280);

        Assert.Equal(DataFileMaxSizeKind.Limited, kind);
        Assert.Equal(10.00m, maxSizeMb);
    }

    [Fact]
    public void MapRow_MapsCoreFields_AndOptionalVolume()
    {
        var row = DataFileHeadroomQuery.MapRow(
            fileId: 1,
            fileName: "Hangfire",
            currentSizeMb: 100m,
            usedMb: 40m,
            freeMb: 60m,
            usedPercent: 40m,
            maxSizePages: -1L,
            growthMb: 64m,
            growthPercent: null,
            isPercentGrowth: false,
            volumeMountPoint: "D:\\Data\\",
            volumeTotalGb: 81m,
            volumeFreeGb: 38.5m,
            volumeFreePercent: 47.5m);

        Assert.Equal(1, row.FileId);
        Assert.Equal("Hangfire", row.FileName);
        Assert.Equal(100m, row.CurrentSizeMB);
        Assert.Equal(DataFileMaxSizeKind.Unlimited, row.MaxSizeKind);
        Assert.Null(row.MaxSizeMB);
        Assert.Equal(64m, row.GrowthMB);
        Assert.False(row.IsPercentGrowth);
        Assert.Equal("D:\\Data\\", row.VolumeMountPoint);
        Assert.Equal(47.5m, row.VolumeFreePercent);
    }

    [Fact]
    public void MapRow_WhenVolumeNull_PreservesNulls()
    {
        var row = DataFileHeadroomQuery.MapRow(
            fileId: 1,
            fileName: "Hangfire",
            currentSizeMb: 10m,
            usedMb: 5m,
            freeMb: 5m,
            usedPercent: 50m,
            maxSizePages: 1280L,
            growthMb: 1m,
            growthPercent: null,
            isPercentGrowth: false,
            volumeMountPoint: null,
            volumeTotalGb: null,
            volumeFreeGb: null,
            volumeFreePercent: null);

        Assert.Equal(DataFileMaxSizeKind.Limited, row.MaxSizeKind);
        Assert.Equal(10.00m, row.MaxSizeMB);
        Assert.Null(row.VolumeMountPoint);
        Assert.Null(row.VolumeTotalGB);
        Assert.Null(row.VolumeFreeGB);
        Assert.Null(row.VolumeFreePercent);
    }
}
