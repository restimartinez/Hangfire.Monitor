using Hangfire.Monitor.Domain;

namespace Hangfire.Monitor.Tests;

public class StorageHealthDiagnosisBuilderTests
{
    [Fact]
    public void BuildDiagnosis_Healthy_ReturnsStandardMessage()
    {
        var headroom = new DataFileHeadroomHealthRules().Evaluate(
        [
            UnlimitedFile()
        ]);

        var diagnosis = StorageHealthDiagnosisBuilder.BuildDiagnosis(
            StorageHealthStatus.OK,
            headroom,
            failureReason: null);

        Assert.Equal(StorageHealthDiagnosisBuilder.HealthyDiagnosis, diagnosis);
    }

    [Fact]
    public void BuildDiagnosis_Unavailable_IncludesFailureReason()
    {
        var diagnosis = StorageHealthDiagnosisBuilder.BuildDiagnosis(
            StorageHealthStatus.UNAVAILABLE,
            headroom: null,
            failureReason: "timeout connecting");

        Assert.Contains("timeout connecting", diagnosis, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildResolution_OnlyWhenWarning()
    {
        Assert.Null(StorageHealthDiagnosisBuilder.BuildResolution(
            StorageHealthStatus.OK,
            headroom: null,
            databaseNameHint: null));

        var warningHeadroom = new DataFileHeadroomHealthRules().Evaluate(
        [
            new DataFileHeadroomFileMetrics(
                1,
                "HangfireData",
                100m,
                100m,
                0m,
                100m,
                DataFileMaxSizeKind.Limited,
                100m,
                64m,
                null,
                false,
                null,
                null,
                null,
                null)
        ]);

        var guide = StorageHealthDiagnosisBuilder.BuildResolution(
            StorageHealthStatus.WARNING,
            warningHeadroom,
            databaseNameHint: null);

        Assert.NotNull(guide);
        Assert.Contains("sys.database_files", guide!.DiagnosticQueriesSql, StringComparison.Ordinal);
        Assert.Contains("dm_os_volume_stats", guide.DiagnosticQueriesSql, StringComparison.Ordinal);
        Assert.Contains("ALTER DATABASE", guide.CorrectiveQueriesSql, StringComparison.Ordinal);
        Assert.Contains("NEVER executes", guide.CorrectiveQueriesSql, StringComparison.Ordinal);
        Assert.Contains("<new_size_mb>", guide.CorrectiveQueriesSql, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=", guide.CorrectiveQueriesSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User ID=", guide.CorrectiveQueriesSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildResolution_EscapesIdentifiers_WhenNamesKnown()
    {
        var warningHeadroom = new DataFileHeadroomHealthRules().Evaluate(
        [
            new DataFileHeadroomFileMetrics(
                1,
                "Weird]Name",
                100m,
                100m,
                0m,
                100m,
                DataFileMaxSizeKind.NoGrowth,
                null,
                64m,
                null,
                false,
                null,
                null,
                null,
                null)
        ]);

        var guide = StorageHealthDiagnosisBuilder.BuildResolution(
            StorageHealthStatus.WARNING,
            warningHeadroom,
            databaseNameHint: "Db]Name");

        Assert.NotNull(guide);
        Assert.Contains("[Db]]Name]", guide!.CorrectiveQueriesSql, StringComparison.Ordinal);
        Assert.Contains("N'Weird]Name'", guide.CorrectiveQueriesSql, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlIdentifierEscaper_Bracket_DoublesClosingBracket()
    {
        Assert.Equal("[a]]b]", SqlIdentifierEscaper.Bracket("a]b"));
    }

    [Fact]
    public void SqlIdentifierEscaper_UnicodeLiteral_DoublesQuotes()
    {
        Assert.Equal("N'O''Brien'", SqlIdentifierEscaper.UnicodeLiteral("O'Brien"));
    }

    private static DataFileHeadroomFileMetrics UnlimitedFile() =>
        new(
            1,
            "data",
            100m,
            50m,
            50m,
            50m,
            DataFileMaxSizeKind.Unlimited,
            null,
            64m,
            null,
            false,
            "C:\\",
            80m,
            40m,
            50m);
}
