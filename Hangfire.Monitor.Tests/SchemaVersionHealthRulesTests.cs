using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class SchemaVersionHealthRulesTests
{
    private readonly SchemaVersionHealthRules _rules = new();

    [Fact]
    public void Evaluate_WhenVersionMatches_ReturnsOk()
    {
        var result = _rules.Evaluate(actualVersion: 9, expectedVersion: 9);

        Assert.Equal(9, result.ActualVersion);
        Assert.Equal(9, result.ExpectedVersion);
        Assert.Equal(StorageHealthStatus.OK, result.Status);
        Assert.Equal("Schema version 9 matches the expected version.", result.Diagnosis);
        Assert.Equal("No action required.", result.Recommendation);
    }

    [Fact]
    public void Evaluate_WhenVersionIsLower_ReturnsWarning()
    {
        var result = _rules.Evaluate(actualVersion: 8, expectedVersion: 9);

        Assert.Equal(8, result.ActualVersion);
        Assert.Equal(9, result.ExpectedVersion);
        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
        Assert.Equal(
            "Schema version 8 is lower than the expected version 9.",
            result.Diagnosis);
        Assert.Equal(
            "Review the Hangfire SQL Server schema before upgrading or using features that require the newer schema version.",
            result.Recommendation);
    }

    [Fact]
    public void Evaluate_WhenVersionIsHigher_ReturnsWarning()
    {
        var result = _rules.Evaluate(actualVersion: 10, expectedVersion: 9);

        Assert.Equal(10, result.ActualVersion);
        Assert.Equal(9, result.ExpectedVersion);
        Assert.Equal(StorageHealthStatus.WARNING, result.Status);
        Assert.Equal(
            "Schema version 10 is higher than the expected version 9.",
            result.Diagnosis);
        Assert.Equal(
            "Review the Hangfire.SqlServer package/schema compatibility before making further changes.",
            result.Recommendation);
    }

    [Fact]
    public void Evaluate_WhenVersionIsNull_ReturnsUnavailable()
    {
        var result = _rules.Evaluate(actualVersion: null, expectedVersion: 9);

        Assert.Null(result.ActualVersion);
        Assert.Equal(9, result.ExpectedVersion);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
        Assert.Equal(
            "A valid Hangfire schema version could not be obtained.",
            result.Diagnosis);
        Assert.Equal(
            "Verify that the Hangfire SQL Server schema is installed and the configured schema name is correct.",
            result.Recommendation);
    }

    [Fact]
    public void Unavailable_ReturnsUnavailableStatus()
    {
        var result = _rules.Unavailable(expectedVersion: 9);

        Assert.Null(result.ActualVersion);
        Assert.Equal(9, result.ExpectedVersion);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, result.Status);
        Assert.Equal(
            "A valid Hangfire schema version could not be obtained.",
            result.Diagnosis);
        Assert.Equal(
            "Verify that the Hangfire SQL Server schema is installed and the configured schema name is correct.",
            result.Recommendation);
    }

    [Fact]
    public void DefaultExpectedSchemaVersion_IsNine()
    {
        Assert.Equal(9, SchemaVersionHealthRules.DefaultExpectedSchemaVersion);
    }
}
