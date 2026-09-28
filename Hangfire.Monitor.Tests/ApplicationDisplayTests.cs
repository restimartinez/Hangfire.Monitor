using Hangfire.Monitor.Web;

namespace Hangfire.Monitor.Tests;

public class ApplicationDisplayTests
{
    [Fact]
    public void FormatLabel_WhenVersionIsNull_ReturnsNameOnly()
    {
        Assert.Equal("Payments.Worker", ApplicationDisplay.FormatLabel("Payments.Worker", null));
    }

    [Fact]
    public void FormatLabel_WhenVersionIsEmpty_ReturnsNameOnly()
    {
        Assert.Equal("Payments.Worker", ApplicationDisplay.FormatLabel("Payments.Worker", ""));
    }

    [Fact]
    public void FormatLabel_WhenVersionIsWhitespace_ReturnsNameOnly()
    {
        Assert.Equal("Payments.Worker", ApplicationDisplay.FormatLabel("Payments.Worker", "   "));
    }

    [Fact]
    public void FormatLabel_WhenVersionIsPresent_ReturnsNameAndVersion()
    {
        Assert.Equal("Payments.Worker (1.8.25)", ApplicationDisplay.FormatLabel("Payments.Worker", "1.8.25"));
    }

    [Fact]
    public void FormatLabel_WhenVersionHasSurroundingWhitespace_TrimsVersion()
    {
        Assert.Equal("App (1.8.14)", ApplicationDisplay.FormatLabel("App", "  1.8.14  "));
    }

    [Fact]
    public void FormatLabel_WhenApplicationNameIsNull_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => ApplicationDisplay.FormatLabel(null!, "1.8.25"));
    }

    [Fact]
    public void FormatLabel_WhenApplicationNameIsWhitespace_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => ApplicationDisplay.FormatLabel("  ", "1.8.25"));
    }
}
