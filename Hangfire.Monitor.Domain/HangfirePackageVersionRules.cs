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
    /// <item>Exact match → <see cref="StorageHealthStatus.Ok"/></item>
    /// <item>Same major.minor, different patch → <see cref="StorageHealthStatus.Warning"/></item>
    /// <item>Lower major.minor than expected → <see cref="StorageHealthStatus.Critical"/></item>
    /// <item>Higher major.minor than expected → <see cref="StorageHealthStatus.Warning"/></item>
    /// <item>Missing or unparseable actual/expected → <see cref="StorageHealthStatus.Unavailable"/></item>
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
                StorageHealthStatus.Unavailable);
        }

        if (!HangfirePackageVersion.TryParse(actualRaw, out var actual))
        {
            return new HangfirePackageVersionResult(
                actualRaw,
                expected.ToString(),
                StorageHealthStatus.Unavailable);
        }

        if (actual.Equals(expected))
        {
            return new HangfirePackageVersionResult(
                actual.ToString(),
                expected.ToString(),
                StorageHealthStatus.Ok);
        }

        if (actual.SameReleaseLineAs(expected))
        {
            return new HangfirePackageVersionResult(
                actual.ToString(),
                expected.ToString(),
                StorageHealthStatus.Warning);
        }

        if (actual.CompareTo(expected) < 0)
        {
            return new HangfirePackageVersionResult(
                actual.ToString(),
                expected.ToString(),
                StorageHealthStatus.Critical);
        }

        return new HangfirePackageVersionResult(
            actual.ToString(),
            expected.ToString(),
            StorageHealthStatus.Warning);
    }
}
