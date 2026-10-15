namespace YogaMarketplace.Api.Middleware;

/// <summary>
/// Ends a request the client gave up on with 499 and one Information line, instead of a 500 and an error.
/// Any other exception, or a cancellation while the client is still waiting, goes on to the error handler unchanged.
/// </summary>
public class ClientAbortMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ClientAbortMiddleware> _logger;

    public ClientAbortMiddleware(RequestDelegate next, ILogger<ClientAbortMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (ClientAbort.IsAbortedBy(context, ex))
        {
            _logger.LogInformation("Request {Method} {Path} was cancelled by the client.", context.Request.Method, context.Request.Path.Value);
            if (!context.Response.HasStarted)
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
    }
}
