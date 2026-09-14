using System.Text;
using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Web.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hangfire.Monitor.Tests;

public class HangfireMonitorOptionsValidatorTests
{
    private readonly HangfireMonitorOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidApplication_Succeeds()
    {
        var options = new HangfireMonitorOptions
        {
            Applications =
            [
                new HangfireApplicationOptions
                {
                    Name = "Billing",
                    ConnectionString = "Server=localhost;Database=HangfireBilling;",
                    Schema = "HangFire"
                }
            ]
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_EmptyName_FailsWithUsefulMessage()
    {
        var options = OptionsWithApplication(name: "", connectionString: "Server=.;Database=Hangfire;");

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("index 0", StringComparison.Ordinal)
            && f.Contains("Name", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhitespaceName_FailsWithUsefulMessage()
    {
        var options = OptionsWithApplication(name: "   ", connectionString: "Server=.;Database=Hangfire;");

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("index 0", StringComparison.Ordinal)
            && f.Contains("Name", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EmptyConnectionString_FailsWithUsefulMessage()
    {
        var options = OptionsWithApplication(name: "Billing", connectionString: "");

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("'Billing'", StringComparison.Ordinal)
            && f.Contains("ConnectionString", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhitespaceConnectionString_FailsWithUsefulMessage()
    {
        var options = OptionsWithApplication(name: "Billing", connectionString: "   ");

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("'Billing'", StringComparison.Ordinal)
            && f.Contains("ConnectionString", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_MultipleApplications_ValidatesEachApplication()
    {
        var options = new HangfireMonitorOptions
        {
            Applications =
            [
                new HangfireApplicationOptions
                {
                    Name = "ValidApp",
                    ConnectionString = "Server=.;Database=Valid;"
                },
                new HangfireApplicationOptions
                {
                    Name = "",
                    ConnectionString = "Server=.;Database=MissingName;"
                },
                new HangfireApplicationOptions
                {
                    Name = "MissingConnection",
                    ConnectionString = ""
                }
            ]
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("index 1", StringComparison.Ordinal)
            && f.Contains("Name", StringComparison.Ordinal));
        Assert.Contains(result.Failures, f => f.Contains("'MissingConnection'", StringComparison.Ordinal)
            && f.Contains("ConnectionString", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EmptyApplications_Succeeds()
    {
        var options = new HangfireMonitorOptions { Applications = [] };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void BindAndValidate_OmittedSchema_DefaultsToHangFire()
    {
        const string json =
            """
            {
              "HangfireMonitor": {
                "Applications": [
                  {
                    "Name": "Billing",
                    "ConnectionString": "Server=localhost;Database=HangfireBilling;"
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
        services
            .AddOptions<HangfireMonitorOptions>()
            .Bind(configuration.GetSection("HangfireMonitor"))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<HangfireMonitorOptions>, HangfireMonitorOptionsValidator>();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HangfireMonitorOptions>>().Value;

        Assert.Single(options.Applications);
        Assert.Equal(HangfireApplicationOptions.DefaultSchema, options.Applications[0].Schema);
        Assert.Equal("HangFire", options.Applications[0].Schema);
    }

    [Fact]
    public void Options_InvalidConfiguration_ThrowsOptionsValidationException()
    {
        const string json =
            """
            {
              "HangfireMonitor": {
                "Applications": [
                  {
                    "Name": "",
                    "ConnectionString": ""
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
        services
            .AddOptions<HangfireMonitorOptions>()
            .Bind(configuration.GetSection("HangfireMonitor"))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<HangfireMonitorOptions>, HangfireMonitorOptionsValidator>();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HangfireMonitorOptions>>();

        var exception = Assert.Throws<OptionsValidationException>(() => _ = options.Value);

        Assert.Contains(exception.Failures, f => f.Contains("Name", StringComparison.Ordinal));
        Assert.Contains(exception.Failures, f => f.Contains("ConnectionString", StringComparison.Ordinal));
    }

    private static HangfireMonitorOptions OptionsWithApplication(string name, string connectionString) =>
        new()
        {
            Applications =
            [
                new HangfireApplicationOptions
                {
                    Name = name,
                    ConnectionString = connectionString
                }
            ]
        };
}
