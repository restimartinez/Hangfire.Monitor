using System.Reflection;
using Hangfire;
using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Infrastructure.Storage;
using Hangfire.SqlServer;

namespace Hangfire.Monitor.Tests;

public class SqlServerStorageFactoryTests
{
    private const string ExampleConnectionString =
        "Server=localhost;Database=ExampleHangfire;Trusted_Connection=True;TrustServerCertificate=True;";

    private readonly SqlServerStorageFactory _factory = new();

    [Fact]
    public void Create_UsesConnectionStringFromApplicationOptions()
    {
        var application = new HangfireApplicationOptions
        {
            Name = "Example",
            ConnectionString = ExampleConnectionString,
            Schema = HangfireApplicationOptions.DefaultSchema
        };

        var storage = _factory.Create(application);

        Assert.Equal(ExampleConnectionString, GetConnectionString(storage));
        // Factory must not register a global storage; Current throws until explicitly set.
        Assert.Throws<InvalidOperationException>(() => _ = JobStorage.Current);
    }

    [Fact]
    public void Create_UsesSchemaFromApplicationOptions()
    {
        var application = new HangfireApplicationOptions
        {
            Name = "Example",
            ConnectionString = ExampleConnectionString,
            Schema = HangfireApplicationOptions.DefaultSchema
        };

        var storage = _factory.Create(application);
        var options = GetStorageOptions(storage);

        Assert.Equal(HangfireApplicationOptions.DefaultSchema, options.SchemaName);
        Assert.Equal("HangFire", options.SchemaName);
    }

    [Fact]
    public void Create_SetsPrepareSchemaIfNecessaryToFalse()
    {
        var application = new HangfireApplicationOptions
        {
            Name = "Example",
            ConnectionString = ExampleConnectionString
        };

        var storage = _factory.Create(application);
        var options = GetStorageOptions(storage);

        Assert.False(options.PrepareSchemaIfNecessary);
    }

    [Fact]
    public void Create_UsesCustomSchema_WhenConfigured()
    {
        var application = new HangfireApplicationOptions
        {
            Name = "CustomApp",
            ConnectionString = ExampleConnectionString,
            Schema = "CustomHangfire"
        };

        var storage = _factory.Create(application);
        var options = GetStorageOptions(storage);

        Assert.Equal("CustomHangfire", options.SchemaName);
        Assert.False(options.PrepareSchemaIfNecessary);
    }

    [Fact]
    public void Create_UsesDefaultSchema_WhenSchemaNotSetOnApplication()
    {
        var application = new HangfireApplicationOptions
        {
            Name = "Example",
            ConnectionString = ExampleConnectionString
        };

        var storage = _factory.Create(application);
        var options = GetStorageOptions(storage);

        Assert.Equal(HangfireApplicationOptions.DefaultSchema, options.SchemaName);
    }

    private static string GetConnectionString(SqlServerStorage storage)
    {
        var field = typeof(SqlServerStorage).GetField(
            "_connectionString",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        return Assert.IsType<string>(field.GetValue(storage));
    }

    private static SqlServerStorageOptions GetStorageOptions(SqlServerStorage storage)
    {
        var property = typeof(SqlServerStorage).GetProperty(
            "Options",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(property);
        return Assert.IsType<SqlServerStorageOptions>(property.GetValue(storage));
    }
}
