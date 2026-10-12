using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;

namespace YogaMarketplace.Api.Middleware;

/// <summary>
/// JSON bodies for failures that never reach a controller: unhandled exceptions and empty error statuses.
/// The client shows <c>error</c>; <c>traceId</c> lets support find the matching log line.
/// </summary>
public static class ApiErrorResponses
{
    public const string UnexpectedMessage = "Something went wrong on our side. Try again in a moment.";

    public static IApplicationBuilder UseApiErrorResponses(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(errorApp => errorApp.Run(WriteUnexpectedAsync));
        app.UseStatusCodePages(context => WriteStatusAsync(context.HttpContext));
        return app;
    }

    private static Task WriteUnexpectedAsync(HttpContext context)
    {
        if (context.Features.Get<IExceptionHandlerFeature>()?.Error is OperationCanceledException
            && context.RequestAborted.IsCancellationRequested)
            return Task.CompletedTask;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return context.Response.WriteAsJsonAsync(new { error = UnexpectedMessage, traceId = TraceId(context) });
    }

    private static Task WriteStatusAsync(HttpContext context)
    {
        var status = context.Response.StatusCode;
        var message = status switch
        {
            StatusCodes.Status401Unauthorized => "Sign in to continue.",
            StatusCodes.Status403Forbidden => "You don't have access to this.",
            StatusCodes.Status404NotFound => "Not found.",
            StatusCodes.Status429TooManyRequests => "Too many requests. Wait a moment and try again.",
            _ => ReasonPhrases.GetReasonPhrase(status)
        };
        return context.Response.WriteAsJsonAsync(new { error = message, traceId = TraceId(context) });
    }

    private static string TraceId(HttpContext context) => Activity.Current?.Id ?? context.TraceIdentifier;
}
