using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class HangfirePackageVersionTests
{
    [Theory]
    [InlineData("1.8.25", 1, 8, 25)]
    [InlineData(" 1.7.0 ", 1, 7, 0)]
    [InlineData("0.0.0", 0, 0, 0)]
    public void TryParse_Valid_ReturnsParts(string text, int major, int minor, int patch)
    {
        Assert.True(HangfirePackageVersion.TryParse(text, out var version));
        Assert.Equal(new HangfirePackageVersion(major, minor, patch), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1.8")]
    [InlineData("1.8.25.1")]
    [InlineData("1.8.25-beta")]
    [InlineData("a.b.c")]
    [InlineData("-1.8.25")]
    public void TryParse_Invalid_ReturnsFalse(string? text)
    {
        Assert.False(HangfirePackageVersion.TryParse(text, out _));
    }

    [Fact]
    public void SameReleaseLineAs_ComparesMajorMinorOnly()
    {
        var left = new HangfirePackageVersion(1, 8, 14);
        var right = new HangfirePackageVersion(1, 8, 25);

        Assert.True(left.SameReleaseLineAs(right));
        Assert.False(left.SameReleaseLineAs(new HangfirePackageVersion(1, 7, 14)));
    }

    [Fact]
    public void CompareTo_OrdersByMajorMinorPatch()
    {
        var older = new HangfirePackageVersion(1, 7, 33);
        var mid = new HangfirePackageVersion(1, 8, 14);
        var latest = new HangfirePackageVersion(1, 8, 25);

        Assert.True(older.CompareTo(mid) < 0);
        Assert.True(mid.CompareTo(latest) < 0);
        Assert.Equal(0, latest.CompareTo(latest));
    }
}
