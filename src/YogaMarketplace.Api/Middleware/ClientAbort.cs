using Microsoft.Data.SqlClient;

namespace YogaMarketplace.Api.Middleware;

/// <summary>
/// Tells a request the client gave up on (tab closed, network lost) apart from a real failure. Both conditions must hold:
/// the request's own token is cancelled and the exception is a cancellation.
/// </summary>
public static class ClientAbort
{
    public static bool IsAbortedBy(HttpContext? context, Exception? exception) =>
        context is { RequestAborted.IsCancellationRequested: true } && exception is not null && IsCancellation(exception);

    /// <summary>
    /// An <see cref="OperationCanceledException"/> anywhere in the chain, or the <see cref="SqlException"/> SqlClient
    /// throws when a running command is cancelled. That one carries only error number 0; real SQL Server errors have a
    /// number, so a deadlock or a timeout is never treated as a cancellation.
    /// </summary>
    public static bool IsCancellation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is OperationCanceledException)
                return true;
            if (current is SqlException sql)
                return sql.Errors.Count > 0 && sql.Errors.Cast<SqlError>().All(e => e.Number == 0);
            if (current is AggregateException aggregate)
                return aggregate.InnerExceptions.Any(IsCancellation);
        }
        return false;
    }
}
