using Hangfire.Monitor.Domain;
using Microsoft.Extensions.Options;

namespace Hangfire.Monitor.Web.Configuration;

/// <summary>
/// Validates <see cref="HangfireMonitorOptions"/> at options evaluation / host startup.
/// </summary>
public sealed class HangfireMonitorOptionsValidator : IValidateOptions<HangfireMonitorOptions>
{
    public ValidateOptionsResult Validate(string? name, HangfireMonitorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Applications is null || options.Applications.Count == 0)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        for (var i = 0; i < options.Applications.Count; i++)
        {
            var application = options.Applications[i];
            var applicationLabel = string.IsNullOrWhiteSpace(application.Name)
                ? $"at index {i}"
                : $"'{application.Name}'";

            if (string.IsNullOrWhiteSpace(application.Name))
            {
                failures.Add($"Hangfire application {applicationLabel} requires a Name.");
            }

            if (string.IsNullOrWhiteSpace(application.ConnectionString))
            {
                failures.Add($"Hangfire application {applicationLabel} requires a ConnectionString.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
