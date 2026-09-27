namespace Hangfire.Monitor.Domain;

/// <summary>
/// Interpreted <c>sys.database_files.max_size</c> for a data file.
/// </summary>
public enum DataFileMaxSizeKind
{
    /// <summary><c>max_size = -1</c> — grow until the volume is full.</summary>
    Unlimited,

    /// <summary><c>max_size = 0</c> — file cannot grow beyond its current size.</summary>
    NoGrowth,

    /// <summary>Finite <c>max_size</c> expressed in megabytes.</summary>
    Limited
}
