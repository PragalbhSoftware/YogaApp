using Microsoft.Extensions.Primitives;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Middleware;

/// <summary>
/// Gives every request one id that shows on its log lines, in the <c>X-Correlation-Id</c> response header and in
/// error bodies. A caller's id is kept when it is short and plain, so it cannot inject text into the logs.
/// The id is also the request's <see cref="HttpContext.TraceIdentifier"/>.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string LogProperty = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var id = Accept(context.Request.Headers[HeaderName]) ?? Guid.NewGuid().ToString("N");
        context.TraceIdentifier = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { [LogProperty] = id }))
            await _next(context);
    }

    internal static string? Accept(StringValues values)
    {
        if (values.Count != 1 || values[0] is not { Length: > 0 and <= ErrorEvent.CorrelationIdMax } id)
            return null;
        return id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') ? id : null;
    }
}
