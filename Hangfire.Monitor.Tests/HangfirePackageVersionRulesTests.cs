using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class HangfirePackageVersionRulesTests
{
    private readonly HangfirePackageVersionRules _rules = new();

    [Fact]
    public void Evaluate_ExactMatch_ReturnsOk()
    {
        var result = _rules.Evaluate("1.8.25", "1.8.25");

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Equal("1.8.25", result.ActualVersion);
        Assert.Equal("1.8.25", result.ExpectedVersion);
    }

    [Theory]
    [InlineData("1.8.14")]
    [InlineData("1.8.0")]
    [InlineData("1.8.26")]
    public void Evaluate_SameReleaseLine_DifferentPatch_ReturnsWarning(string actual)
    {
        var result = _rules.Evaluate(actual, "1.8.25");

        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
    }

    [Theory]
    [InlineData("1.7.33")]
    [InlineData("1.6.0")]
    [InlineData("0.9.1")]
    public void Evaluate_OlderReleaseLine_ReturnsCritical(string actual)
    {
        var result = _rules.Evaluate(actual, "1.8.25");

        Assert.Equal(StorageHealthStatus.CRITICAL, result.Status);
    }

    [Theory]
    [InlineData("1.9.0")]
    [InlineData("2.0.0")]
    public void Evaluate_NewerReleaseLine_ReturnsWarning(string actual)
    {
        var result = _rules.Evaluate(actual, "1.8.25");

        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    public void Evaluate_MissingOrInvalidActual_ReturnsUnavailable(string? actual)
    {
        var result = _rules.Evaluate(actual, "1.8.25");

        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.8")]
    public void Evaluate_MissingOrInvalidExpected_ReturnsUnavailable(string? expected)
    {
        var result = _rules.Evaluate("1.8.25", expected);

        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
    }

    [Fact]
    public void Evaluate_TrimsWhitespace()
    {
        var result = _rules.Evaluate(" 1.8.25 ", " 1.8.25 ");

        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Equal("1.8.25", result.ActualVersion);
        Assert.Equal("1.8.25", result.ExpectedVersion);
    }
}
