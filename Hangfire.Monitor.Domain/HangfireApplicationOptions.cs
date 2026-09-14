namespace Hangfire.Monitor.Domain;

/// <summary>
/// Configuration for a single Hangfire application to monitor.
/// </summary>
public class HangfireApplicationOptions
{
    public const string DefaultSchema = "HangFire";

    /// <summary>
    /// Human-readable application label. Required (non-whitespace; validated at startup).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Hangfire SQL Server connection string. Required (non-whitespace; validated at startup).
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Hangfire SQL schema name. Optional; defaults to <see cref="DefaultSchema"/> when unset.
    /// </summary>
    public string Schema { get; set; } = DefaultSchema;
}
