using System.Data.Common;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hangfire.SqlServer;

namespace Hangfire.Monitor.Infrastructure.Storage;

/// <summary>
/// Narrow access to Hangfire.SqlServer 1.8.25 connection/schema helpers that are not public.
/// </summary>
/// <remarks>
/// Public alternatives considered and rejected for HM-031:
/// <list type="bullet">
/// <item><see cref="SqlServerStorage.GetConnection"/> / <c>GetReadOnlyConnection</c> return
/// Hangfire's <c>IStorageConnection</c> abstraction — no raw SQL execution surface.</item>
/// <item>Opening a new ADO.NET connection ourselves would require adding a SQL client package
/// and would bypass Hangfire's factory, existing-connection, and impersonation handling.</item>
/// </list>
/// Therefore this type invokes the internal <c>UseConnection</c> / <c>Options</c> members that
/// Hangfire's own monitoring path uses. Reflection is limited to those two seams.
/// </remarks>
internal static class SqlServerStorageDb
{
    private static readonly MethodInfo UseConnectionMethod = ResolveUseConnectionMethod();
    private static readonly PropertyInfo OptionsProperty = ResolveOptionsProperty();

    public static string GetSchemaName(SqlServerStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var options = (SqlServerStorageOptions?)OptionsProperty.GetValue(storage)
            ?? throw new InvalidOperationException("SqlServerStorage.Options was null.");

        return options.SchemaName;
    }

    public static TResult UseConnection<TResult>(
        SqlServerStorage storage,
        Func<DbConnection, TResult> action)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(action);

        var typed = UseConnectionMethod.MakeGenericMethod(typeof(TResult));
        Func<SqlServerStorage, DbConnection, TResult> hangfireAction =
            (_, connection) => action(connection);

        try
        {
            var result = typed.Invoke(storage, [null, hangfireAction]);
            return (TResult)result!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static MethodInfo ResolveUseConnectionMethod()
    {
        var methods = typeof(SqlServerStorage)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(m => m.Name == "UseConnection" && m.IsGenericMethodDefinition)
            .ToArray();

        // UseConnection<TResult>(DbConnection dedicatedConnection, Func<SqlServerStorage, DbConnection, TResult> action)
        var match = methods.FirstOrDefault(m =>
        {
            var parameters = m.GetParameters();
            if (parameters.Length != 2)
            {
                return false;
            }

            if (parameters[0].ParameterType != typeof(DbConnection))
            {
                return false;
            }

            var funcType = parameters[1].ParameterType;
            return funcType.IsGenericType
                && funcType.GetGenericTypeDefinition() == typeof(Func<,,>)
                && funcType.GenericTypeArguments[0] == typeof(SqlServerStorage)
                && funcType.GenericTypeArguments[1] == typeof(DbConnection);
        });

        return match
            ?? throw new InvalidOperationException(
                "Could not locate SqlServerStorage.UseConnection<TResult>(DbConnection, Func<SqlServerStorage, DbConnection, TResult>).");
    }

    private static PropertyInfo ResolveOptionsProperty()
    {
        return typeof(SqlServerStorage).GetProperty(
                   "Options",
                   BindingFlags.Instance | BindingFlags.NonPublic)
               ?? throw new InvalidOperationException("Could not locate SqlServerStorage.Options.");
    }
}
