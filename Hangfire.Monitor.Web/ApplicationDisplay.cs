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

    /// <summary>
    /// Returns the trimmed Hangfire package version, or <c>-</c> when missing/whitespace.
    /// </summary>
    public static string FormatVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "-";
        }

        return version.Trim();
    }

    /// <summary>
    /// Sort key for the Version column: trimmed version, or empty when missing/whitespace.
    /// </summary>
    public static string FormatVersionSortValue(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        return version.Trim();
    }
}
