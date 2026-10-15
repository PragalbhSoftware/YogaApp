using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using YogaMarketplace.Api.Middleware;

namespace YogaMarketplace.Api.Logging;

/// <summary>
/// Drops Error events that only say the current request's client went away. Kept narrow: Error level only, only while
/// the request on this call is aborted, and only when the logged exception is a cancellation. The one exception-less
/// event dropped is EF Core's "Failed executing DbCommand", which never carries the exception; when the failure was
/// real, EF Core's query or save failure and the error handler still log it with the exception, so it is recorded.
/// </summary>
public class ClientAbortFilter : ILogEventFilter
{
    private readonly IHttpContextAccessor _http;

    public ClientAbortFilter(IHttpContextAccessor http)
    {
        _http = http;
    }

    public bool IsEnabled(LogEvent logEvent)
    {
        if (logEvent.Level != LogEventLevel.Error || _http.HttpContext is not { RequestAborted.IsCancellationRequested: true })
            return true;
        return logEvent.Exception is null
            ? !IsEfCommandError(logEvent)
            : !ClientAbort.IsCancellation(logEvent.Exception);
    }

    private static bool IsEfCommandError(LogEvent logEvent) =>
        logEvent.Properties.TryGetValue("EventId", out var value)
        && value is StructureValue eventId
        && eventId.Properties.Any(p => p is { Name: "Id", Value: ScalarValue { Value: int id } } && id == RelationalEventId.CommandError.Id);
}
