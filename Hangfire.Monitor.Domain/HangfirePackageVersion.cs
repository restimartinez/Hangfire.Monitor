namespace Hangfire.Monitor.Domain;

/// <summary>
/// Parsed Hangfire package version (major.minor.patch).
/// </summary>
public readonly record struct HangfirePackageVersion(int Major, int Minor, int Patch)
    : IComparable<HangfirePackageVersion>
{
    public int CompareTo(HangfirePackageVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        if (minor != 0)
        {
            return minor;
        }

        return Patch.CompareTo(other.Patch);
    }

    public bool SameReleaseLineAs(HangfirePackageVersion other) =>
        Major == other.Major && Minor == other.Minor;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    /// <summary>
    /// Parses <c>major.minor.patch</c> (optional leading/trailing whitespace).
    /// Rejects pre-release/build metadata and fewer/more than three numeric parts.
    /// </summary>
    public static bool TryParse(string? text, out HangfirePackageVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Trim().Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var patch))
        {
            return false;
        }

        if (major < 0 || minor < 0 || patch < 0)
        {
            return false;
        }

        version = new HangfirePackageVersion(major, minor, patch);
        return true;
    }
}
