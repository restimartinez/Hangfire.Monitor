namespace Hangfire.Monitor.Domain;

/// <summary>
/// Pure rules that map a configured Hangfire package version into
/// <see cref="HangfirePackageVersionResult"/> for Version-column badges.
/// </summary>
public sealed class HangfirePackageVersionRules
{
    /// <summary>
    /// Compares <paramref name="actualVersion"/> to <paramref name="expectedLatestVersion"/>.
    /// </summary>
    /// <remarks>
    /// Rules (when both sides parse as <c>major.minor.patch</c>):
    /// <list type="bullet">
    /// <item>Exact match → <see cref="StorageHealthStatus.OK"/></item>
    /// <item>Same major.minor, different patch → <see cref="StorageHealthStatus.WARNING"/></item>
    /// <item>Lower major.minor than expected → <see cref="StorageHealthStatus.CRITICAL"/></item>
    /// <item>Higher major.minor than expected → <see cref="StorageHealthStatus.WARNING"/></item>
    /// <item>Missing or unparseable actual/expected → <see cref="StorageHealthStatus.UNAVAILABLE"/></item>
    /// </list>
    /// </remarks>
    public HangfirePackageVersionResult Evaluate(string? actualVersion, string? expectedLatestVersion)
    {
        var expectedRaw = string.IsNullOrWhiteSpace(expectedLatestVersion)
            ? null
            : expectedLatestVersion.Trim();
        var actualRaw = string.IsNullOrWhiteSpace(actualVersion)
            ? null
            : actualVersion.Trim();

        if (!HangfirePackageVersion.TryParse(expectedRaw, out var expected))
        {
            return new HangfirePackageVersionResult(
                actualRaw,
                expectedRaw,
                StorageHealthStatus.UNAVAILABLE);
        }

        if (!HangfirePackageVersion.TryParse(actualRaw, out var actual))
        {
            return new HangfirePackageVersionResult(
                actualRaw,
                expected.ToString(),
                StorageHealthStatus.UNAVAILABLE);
        }

        if (actual.Equals(expected))
        {
            return new HangfirePackageVersionResult(
                actual.ToString(),
                expected.ToString(),
                StorageHealthStatus.OK);
        }

        if (actual.SameReleaseLineAs(expected))
        {
            return new HangfirePackageVersionResult(
                actual.ToString(),
                expected.ToString(),
                StorageHealthStatus.WARNING);
        }

        if (actual.CompareTo(expected) < 0)
        {
            return new HangfirePackageVersionResult(
                actual.ToString(),
                expected.ToString(),
                StorageHealthStatus.CRITICAL);
        }

        return new HangfirePackageVersionResult(
            actual.ToString(),
            expected.ToString(),
            StorageHealthStatus.WARNING);
    }
}
