namespace Hangfire.Monitor.Domain;

/// <summary>
/// Escapes SQL Server delimited identifiers for display in copy/paste guidance.
/// Does not execute SQL and does not validate against a live server.
/// </summary>
public static class SqlIdentifierEscaper
{
    /// <summary>
    /// Wraps <paramref name="identifier"/> in brackets, doubling any <c>]</c> characters.
    /// </summary>
    public static string Bracket(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        return "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }

    /// <summary>
    /// Returns an <c>N'...'</c> string literal with single quotes doubled.
    /// </summary>
    public static string UnicodeLiteral(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return "N'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }
}
