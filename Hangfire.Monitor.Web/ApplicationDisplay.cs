namespace Hangfire.Monitor.Web;

/// <summary>
/// Formats configured application identity for UI display.
/// </summary>
public static class ApplicationDisplay
{
    /// <summary>
    /// Returns <c>Name (version)</c> when <paramref name="version"/> is non-whitespace;
    /// otherwise returns <paramref name="applicationName"/> alone.
    /// </summary>
    public static string FormatLabel(string applicationName, string? version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        if (string.IsNullOrWhiteSpace(version))
        {
            return applicationName;
        }

        return $"{applicationName} ({version.Trim()})";
    }
}
