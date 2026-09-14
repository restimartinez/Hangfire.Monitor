using System.Text;
using Hangfire.Monitor.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hangfire.Monitor.Tests;

public class HangfireMonitorOptionsBindingTests
{
    [Fact]
    public void Configure_BindsHangfireMonitorSection_ToOptions()
    {
        const string json =
            """
            {
              "HangfireMonitor": {
                "Applications": [
                  {
                    "Name": "Billing",
                    "ConnectionString": "Server=localhost;Database=HangfireBilling;",
                    "Schema": "HangFire"
                  }
                ]
              }
            }
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        var services = new ServiceCollection();
        services.Configure<HangfireMonitorOptions>(configuration.GetSection("HangfireMonitor"));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HangfireMonitorOptions>>().Value;

        Assert.Single(options.Applications);
        Assert.Equal("Billing", options.Applications[0].Name);
        Assert.Equal("Server=localhost;Database=HangfireBilling;", options.Applications[0].ConnectionString);
        Assert.Equal("HangFire", options.Applications[0].Schema);
    }
}
