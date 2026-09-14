using Hangfire.Monitor.Domain;
using Hangfire.SqlServer;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Creates a per-application <see cref="SqlServerStorage"/> for read-only monitoring.
/// Does not register <c>JobStorage.Current</c>; each call returns an independent instance.
/// </summary>
public class SqlServerStorageFactory
{
    public SqlServerStorage Create(HangfireApplicationOptions application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var options = new SqlServerStorageOptions
        {
            PrepareSchemaIfNecessary = false,
            SchemaName = application.Schema,
            // Avoid opening a database connection during construction (schema install is already off).
            TryAutoDetectSchemaDependentOptions = false
        };

        return new SqlServerStorage(application.ConnectionString, options);
    }
}
