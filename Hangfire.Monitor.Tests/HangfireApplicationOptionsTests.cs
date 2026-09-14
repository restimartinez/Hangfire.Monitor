using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class HangfireApplicationOptionsTests
{
    [Fact]
    public void Schema_DefaultsToHangFire_WhenNotSet()
    {
        var options = new HangfireApplicationOptions();

        Assert.Equal(HangfireApplicationOptions.DefaultSchema, options.Schema);
        Assert.Equal("HangFire", options.Schema);
    }

    [Fact]
    public void Schema_UsesExplicitValue_WhenProvided()
    {
        var options = new HangfireApplicationOptions
        {
            Name = "App1",
            ConnectionString = "Server=.;Database=Hangfire;",
            Schema = "CustomSchema"
        };

        Assert.Equal("CustomSchema", options.Schema);
    }

    [Fact]
    public void HangfireMonitorOptions_Applications_DefaultsToEmptyList()
    {
        var options = new HangfireMonitorOptions();

        Assert.NotNull(options.Applications);
        Assert.Empty(options.Applications);
    }
}
