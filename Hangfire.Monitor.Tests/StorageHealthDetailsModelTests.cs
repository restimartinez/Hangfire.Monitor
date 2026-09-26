using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Monitoring;
using Hangfire.Monitor.Infrastructure.Storage;
using Hangfire.Monitor.Web.Pages.StorageHealth;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Hangfire.Monitor.Tests;

public class StorageHealthDetailsModelTests
{
    private readonly SchemaVersionHealthRules _schemaRules = new();
    private readonly DataFileSpaceHealthRules _dataFileRules = new();
    private readonly ApplicationStorageHealthRules _appRules = new();

    [Fact]
    public void OnGet_WithConfiguredApplication_ExposesAllDetailValues()
    {
        var dataMetrics = new DataFileSpaceMetrics(12450m, 7320m, 5130m);
        var logSpace = new LogSpaceMetrics(2048m, 320m, 1728m, 15.6m);
        var logReuse = new LogReuseWaitMetrics(0, "NOTHING", "FULL");
        var beginTime = new DateTime(2026, 9, 26, 9, 42, 17, DateTimeKind.Utc);
        var transactions = new ActiveTransactionMetrics(3, beginTime, 1902);
        var application = App("AtentoOnline");
        var options = Options.Create(new HangfireMonitorOptions { Applications = [application] });
        var monitor = CreateMonitor(
            _ => CreateStorage(),
            _ => 9,
            _ => dataMetrics,
            _ => logSpace,
            _ => logReuse,
            _ => transactions,
            _ => 3);

        var model = new DetailsModel(monitor, options);

        var result = model.OnGet("AtentoOnline");

        Assert.IsType<PageResult>(result);
        var details = Assert.IsType<StorageHealthDetails>(model.Details);

        Assert.Equal("AtentoOnline", details.ApplicationName);
        Assert.Equal(3, details.ServerCount);
        Assert.False(details.IsUnavailable);

        Assert.Equal(12450m, details.DataFiles.AllocatedMB);
        Assert.Equal(7320m, details.DataFiles.UsedMB);
        Assert.Equal(5130m, details.DataFiles.FreeMB);
        Assert.Equal(58.80m, details.DataFiles.UsedPercent);

        Assert.Equal(logSpace, details.Log);
        Assert.Equal(2048m, details.Log!.TotalLogMB);
        Assert.Equal(320m, details.Log.UsedLogMB);
        Assert.Equal(1728m, details.Log.FreeLogMB);
        Assert.Equal(15.6m, details.Log.UsedPercent);

        Assert.Equal(logReuse, details.LogReuse);
        Assert.Equal(0, details.LogReuse!.Wait);
        Assert.Equal("NOTHING", details.LogReuse.WaitDescription);
        Assert.Equal("FULL", details.LogReuse.RecoveryModel);

        Assert.Equal(transactions, details.ActiveTransactions);
        Assert.Equal(3, details.ActiveTransactions!.Count);
        Assert.Equal(beginTime, details.ActiveTransactions.OldestBeginTimeUtc);
        Assert.Equal(1902, details.ActiveTransactions.OldestDurationSeconds);
    }

    [Fact]
    public void OnGet_ActiveTransactions_CountOne_DurationZero_PropagatesToPageModel()
    {
        var beginTime = new DateTime(2026, 9, 26, 12, 38, 17, 223, DateTimeKind.Utc);
        var transactions = new ActiveTransactionMetrics(1, beginTime, 0);
        var options = Options.Create(new HangfireMonitorOptions
        {
            Applications = [App("AtentoOnline")]
        });
        var monitor = CreateMonitor(
            _ => CreateStorage(),
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => new LogSpaceMetrics(1m, 0m, 1m, 0m),
            _ => new LogReuseWaitMetrics(0, "NOTHING", "FULL"),
            _ => transactions,
            _ => 1);

        var model = new DetailsModel(monitor, options);

        Assert.IsType<PageResult>(model.OnGet("AtentoOnline"));

        var details = Assert.IsType<StorageHealthDetails>(model.Details);
        Assert.Equal(transactions, details.ActiveTransactions);
        Assert.Equal(1, details.ActiveTransactions!.Count);
        Assert.Equal(beginTime, details.ActiveTransactions.OldestBeginTimeUtc);
        Assert.Equal(0, details.ActiveTransactions.OldestDurationSeconds);

        var display = Hangfire.Monitor.Web.StorageHealthDisplay.FormatActiveTransactionDetails(
            details.ActiveTransactions);
        Assert.Equal("1", display.Count);
        Assert.NotEqual("-", display.OldestBeginTime);
        Assert.Equal("00:00:00", display.OldestDuration);
    }

    [Fact]
    public void OnGet_ActiveTransactions_CountZero_PropagatesNullOldestFields()
    {
        var transactions = new ActiveTransactionMetrics(0, null, null);
        var options = Options.Create(new HangfireMonitorOptions
        {
            Applications = [App("AtentoOnline")]
        });
        var monitor = CreateMonitor(
            _ => CreateStorage(),
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => new LogSpaceMetrics(1m, 0m, 1m, 0m),
            _ => new LogReuseWaitMetrics(0, "NOTHING", "FULL"),
            _ => transactions);

        var model = new DetailsModel(monitor, options);

        Assert.IsType<PageResult>(model.OnGet("AtentoOnline"));

        var details = Assert.IsType<StorageHealthDetails>(model.Details);
        Assert.Equal(0, details.ActiveTransactions!.Count);
        Assert.Null(details.ActiveTransactions.OldestBeginTimeUtc);
        Assert.Null(details.ActiveTransactions.OldestDurationSeconds);

        var display = Hangfire.Monitor.Web.StorageHealthDisplay.FormatActiveTransactionDetails(
            details.ActiveTransactions);
        Assert.Equal(("0", "-", "-"), display);
    }

    [Fact]
    public void OnGet_UnknownApplication_ReturnsNotFound_WithoutCreatingStorage()
    {
        var createStorageCalls = 0;
        var options = Options.Create(new HangfireMonitorOptions
        {
            Applications = [App("Configured.App")]
        });
        var monitor = CreateMonitor(
            _ =>
            {
                createStorageCalls++;
                return CreateStorage();
            },
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => new LogSpaceMetrics(1m, 0m, 1m, 0m),
            _ => new LogReuseWaitMetrics(0, "NOTHING", "FULL"),
            _ => new ActiveTransactionMetrics(0, null, null));

        var model = new DetailsModel(monitor, options);

        var result = model.OnGet("Unknown.App");

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(model.Details);
        Assert.Equal(0, createStorageCalls);
    }

    [Fact]
    public void OnGet_WhitespaceApplicationName_ReturnsNotFound()
    {
        var options = Options.Create(new HangfireMonitorOptions
        {
            Applications = [App("Configured.App")]
        });
        var monitor = CreateMonitor(
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"));

        var model = new DetailsModel(monitor, options);

        Assert.IsType<NotFoundResult>(model.OnGet(" "));
        Assert.IsType<NotFoundResult>(model.OnGet(string.Empty));
        Assert.Null(model.Details);
    }

    [Fact]
    public void OnGet_RouteCannotSupplyArbitraryConnectionString()
    {
        var createStorageCalls = 0;
        HangfireApplicationOptions? received = null;
        var configured = App("Payments.Worker");
        var options = Options.Create(new HangfireMonitorOptions { Applications = [configured] });
        var monitor = CreateMonitor(
            app =>
            {
                createStorageCalls++;
                received = app;
                return CreateStorage();
            },
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => new LogSpaceMetrics(1m, 0m, 1m, 0m),
            _ => new LogReuseWaitMetrics(0, "NOTHING", "FULL"),
            _ => new ActiveTransactionMetrics(0, null, null));

        var model = new DetailsModel(monitor, options);

        // Connection-string-looking route values must not open a database.
        var notFound = model.OnGet("Server=evil;Database=Hack;Trusted_Connection=True;");
        Assert.IsType<NotFoundResult>(notFound);
        Assert.Equal(0, createStorageCalls);

        // Only the configured application identity is accepted; connection comes from options.
        var page = model.OnGet("Payments.Worker");
        Assert.IsType<PageResult>(page);
        Assert.Equal(1, createStorageCalls);
        Assert.Same(configured, received);
        Assert.Equal(configured.ConnectionString, received!.ConnectionString);
    }

    [Fact]
    public void OnGet_UnavailableApplication_FollowsExistingErrorBehaviour()
    {
        var options = Options.Create(new HangfireMonitorOptions
        {
            Applications = [App("Offline.App")]
        });
        var monitor = CreateMonitor(
            _ => throw new FakeDbException("cannot open"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"),
            _ => throw new InvalidOperationException("should not be called"));

        var model = new DetailsModel(monitor, options);

        var result = model.OnGet("Offline.App");

        Assert.IsType<PageResult>(result);
        var details = Assert.IsType<StorageHealthDetails>(model.Details);
        Assert.Equal("Offline.App", details.ApplicationName);
        Assert.True(details.IsUnavailable);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, details.Status);
        Assert.Equal(0, details.ServerCount);
        Assert.Equal(StorageHealthStatus.UNAVAILABLE, details.DataFiles.Status);
        Assert.Null(details.Log);
        Assert.Null(details.LogReuse);
        Assert.Null(details.ActiveTransactions);
        Assert.DoesNotContain(
            "Server=",
            details.ApplicationName,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OnGet_MatchesConfiguredApplicationName_Exactly()
    {
        var options = Options.Create(new HangfireMonitorOptions
        {
            Applications = [App("Payments.Worker")]
        });
        var monitor = CreateSuccessfulMonitor();
        var model = new DetailsModel(monitor, options);

        Assert.IsType<NotFoundResult>(model.OnGet("payments.worker"));
        Assert.IsType<PageResult>(model.OnGet("Payments.Worker"));
        Assert.Equal("Payments.Worker", model.Details!.ApplicationName);
    }

    [Fact]
    public void IndexMarkup_ApplicationName_LinksToDetailsPage()
    {
        var markupPath = Path.Combine(
            FindRepositoryRoot(),
            "Hangfire.Monitor.Web",
            "Pages",
            "StorageHealth",
            "Index.cshtml");
        var markup = File.ReadAllText(markupPath);

        Assert.Contains("asp-page=\"./Details\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-route-applicationName=\"@result.ApplicationName\"", markup, StringComparison.Ordinal);
        Assert.Contains("@result.ApplicationName</a>", markup, StringComparison.Ordinal);
    }

    private ConfiguredApplicationsStorageHealthMonitor CreateSuccessfulMonitor() =>
        CreateMonitor(
            _ => CreateStorage(),
            _ => 9,
            _ => new DataFileSpaceMetrics(100m, 50m, 50m),
            _ => new LogSpaceMetrics(1m, 0m, 1m, 0m),
            _ => new LogReuseWaitMetrics(0, "NOTHING", "FULL"),
            _ => new ActiveTransactionMetrics(0, null, null),
            _ => 1);

    private ConfiguredApplicationsStorageHealthMonitor CreateMonitor(
        Func<HangfireApplicationOptions, SqlServerStorage> createStorage,
        Func<HangfireApplicationOptions, int?> getSchemaVersionForApp,
        Func<HangfireApplicationOptions, DataFileSpaceMetrics> getDataFileSpaceForApp,
        Func<SqlServerStorage, LogSpaceMetrics> getLogSpace,
        Func<SqlServerStorage, LogReuseWaitMetrics> getLogReuseWait,
        Func<SqlServerStorage, ActiveTransactionMetrics> getActiveTransactions,
        Func<SqlServerStorage, long>? getServerCount = null)
    {
        HangfireApplicationOptions? current = null;

        return new ConfiguredApplicationsStorageHealthMonitor(
            app =>
            {
                current = app;
                return createStorage(app);
            },
            _ => getSchemaVersionForApp(current!),
            _ => getDataFileSpaceForApp(current!),
            getLogSpace,
            getLogReuseWait,
            getActiveTransactions,
            getServerCount ?? (_ => 1),
            _schemaRules,
            _dataFileRules,
            _appRules);
    }

    private static SqlServerStorage CreateStorage() =>
        new SqlServerStorageFactory().Create(App("stub-storage"));

    private static HangfireApplicationOptions App(string name) =>
        new()
        {
            Name = name,
            ConnectionString = "Server=localhost;Database=Example;Trusted_Connection=True;",
            Schema = HangfireApplicationOptions.DefaultSchema
        };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hangfire.Monitor.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test base directory.");
    }

    private sealed class FakeDbException : System.Data.Common.DbException
    {
        public FakeDbException(string message)
            : base(message)
        {
        }
    }
}
