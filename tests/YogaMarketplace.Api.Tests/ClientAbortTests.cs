using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Events;
using YogaMarketplace.Api.Logging;
using YogaMarketplace.Api.Middleware;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

/// <summary>
/// A client that disconnects mid-request is not an error. Hosts here have no background error writer; tests drain the
/// error queue themselves. The content page query is held open until the client cancels, so timing never decides.
/// </summary>
public class ClientAbortTests
{
    private const string CancelledLine = "Information Request \"GET\" \"/api/pages/terms\" was cancelled by the client.";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_request_the_client_cancels_logs_information_and_writes_no_error_event(bool asSqlException)
    {
        await using var factory = new YogaApiFactory();
        var interceptor = new HoldingInterceptor { OnCancel = asSqlException ? SqlExceptions.Cancelled : null };
        var (host, logs) = NewHost(factory, interceptor);
        await using var _ = host;
        var client = host.CreateClient();
        await DrainErrorsAsync(host);

        await CancelMidQueryAsync(client, interceptor, "corr-abort");
        await WaitForAsync(() => logs.Lines.Any(l => l.StartsWith(CancelledLine, StringComparison.Ordinal)));

        Assert.DoesNotContain(logs.Lines, l => l.StartsWith("Error", StringComparison.Ordinal) || l.StartsWith("Critical", StringComparison.Ordinal));
        Assert.Equal(0, await DrainErrorsAsync(host));
        Assert.False(await AnyErrorAsync(host, e => e.LastCorrelationId == "corr-abort"));
    }

    [Fact]
    public async Task An_unrelated_error_in_an_aborted_request_is_still_recorded()
    {
        await using var factory = new YogaApiFactory();
        var interceptor = new HoldingInterceptor { OnCancel = () => new InvalidOperationException("Content store is down.") };
        var (host, logs) = NewHost(factory, interceptor);
        await using var _ = host;
        var client = host.CreateClient();
        await DrainErrorsAsync(host);

        await CancelMidQueryAsync(client, interceptor, "corr-abort-unrelated");
        await WaitForAsync(() => logs.Lines.Any(l => l.StartsWith("Error An unhandled exception", StringComparison.Ordinal)));

        Assert.True(await DrainErrorsAsync(host) > 0);
        Assert.True(await AnyErrorAsync(host, e => e.LastCorrelationId == "corr-abort-unrelated"));
        Assert.DoesNotContain(logs.Lines, l => l.StartsWith(CancelledLine, StringComparison.Ordinal));
    }

    [Fact]
    public void Filter_drops_only_cancellation_errors_of_the_aborted_request()
    {
        var accessor = new HttpContextAccessor { HttpContext = Context(aborted: true) };
        var filter = new ClientAbortFilter(accessor);

        Assert.False(filter.IsEnabled(Event(LogEventLevel.Error, new OperationCanceledException())));
        Assert.False(filter.IsEnabled(Event(LogEventLevel.Error, new TaskCanceledException())));
        Assert.False(filter.IsEnabled(Event(LogEventLevel.Error, SqlExceptions.Cancelled())));
        Assert.False(filter.IsEnabled(Event(LogEventLevel.Error, new DbUpdateException("Save failed.", SqlExceptions.Cancelled()))));

        Assert.True(filter.IsEnabled(Event(LogEventLevel.Error, new InvalidOperationException("Content store is down."))));
        Assert.True(filter.IsEnabled(Event(LogEventLevel.Error, SqlExceptions.Create(1205, 13, "Deadlock victim."))));
        Assert.True(filter.IsEnabled(Event(LogEventLevel.Error, null)));
        Assert.True(filter.IsEnabled(Event(LogEventLevel.Error, null, CoreEventId.SaveChangesFailed.Id)));
        Assert.True(filter.IsEnabled(Event(LogEventLevel.Fatal, new OperationCanceledException())));
        Assert.True(filter.IsEnabled(Event(LogEventLevel.Warning, new OperationCanceledException())));
        Assert.False(filter.IsEnabled(Event(LogEventLevel.Error, null, RelationalEventId.CommandError.Id)));

        accessor.HttpContext = Context(aborted: false);
        Assert.True(filter.IsEnabled(Event(LogEventLevel.Error, new OperationCanceledException())));
        Assert.True(filter.IsEnabled(Event(LogEventLevel.Error, null, RelationalEventId.CommandError.Id)));
        accessor.HttpContext = null;
        Assert.True(filter.IsEnabled(Event(LogEventLevel.Error, new OperationCanceledException())));
    }

    [Fact]
    public async Task Middleware_returns_499_only_for_a_cancellation_of_an_aborted_request()
    {
        var aborted = Context(aborted: true);
        await Middleware(SqlExceptions.Cancelled()).InvokeAsync(aborted);
        Assert.Equal(StatusCodes.Status499ClientClosedRequest, aborted.Response.StatusCode);

        await Assert.ThrowsAsync<OperationCanceledException>(() => Middleware(new OperationCanceledException()).InvokeAsync(Context(aborted: false)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Middleware(new InvalidOperationException("Boom")).InvokeAsync(Context(aborted: true)));
    }

    private static ClientAbortMiddleware Middleware(Exception thrown) =>
        new(_ => throw thrown, NullLogger<ClientAbortMiddleware>.Instance);

    private static DefaultHttpContext Context(bool aborted) =>
        new() { RequestAborted = aborted ? new CancellationToken(canceled: true) : CancellationToken.None };

    private static LogEvent Event(LogEventLevel level, Exception? exception, int? eventId = null) =>
        new(DateTimeOffset.UtcNow, level, exception, MessageTemplate.Empty, eventId is int id
            ? [new LogEventProperty("EventId", new StructureValue([new LogEventProperty("Id", new ScalarValue(id))]))]
            : []);

    /// <summary>Sends a request, waits until its content page query is running, then the client gives up.</summary>
    private static async Task CancelMidQueryAsync(HttpClient client, HoldingInterceptor interceptor, string correlationId)
    {
        using var cancel = new CancellationTokenSource();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/pages/terms");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        interceptor.Armed = true;
        var sent = client.SendAsync(request, cancel.Token);
        await interceptor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sent);
    }

    private static async Task WaitForAsync(Func<bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!done())
        {
            Assert.True(DateTime.UtcNow < until, "The aborted request did not finish in time.");
            await Task.Delay(20);
        }
    }

    private static (WebApplicationFactory<Program> Host, LogCapture Logs) NewHost(YogaApiFactory factory, HoldingInterceptor interceptor)
    {
        var logs = new LogCapture();
        var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IInterceptor>(interceptor);
                services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ErrorEventWriter)));
            });
        });
        return (host, logs);
    }

    private static async Task<int> DrainErrorsAsync(WebApplicationFactory<Program> host)
    {
        var queue = host.Services.GetRequiredService<ErrorEventQueue>();
        var writer = ActivatorUtilities.CreateInstance<ErrorEventWriter>(host.Services);
        var written = 0;
        while (queue.Reader.TryRead(out var occurrence))
        {
            await writer.WriteAsync(occurrence, CancellationToken.None);
            written++;
        }
        return written;
    }

    private static async Task<bool> AnyErrorAsync(WebApplicationFactory<Program> host, System.Linq.Expressions.Expression<Func<Domain.ErrorEvent, bool>> match)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<YogaDbContext>().ErrorEvents.AnyAsync(match);
    }

    /// <summary>
    /// Once armed, holds the content page query until its token is cancelled, then fails the way
    /// <see cref="OnCancel"/> says (by default with the cancellation itself, like SQLite and EF Core do).
    /// </summary>
    private sealed class HoldingInterceptor : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public Func<Exception>? OnCancel { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Armed || !command.CommandText.Contains("ContentPages", StringComparison.Ordinal))
                return result;
            Entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException) when (OnCancel is not null)
            {
                throw OnCancel();
            }
            return result;
        }
    }

    /// <summary>Every event Serilog lets through, as "level message | exception".</summary>
    private sealed class LogCapture : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(Lines);

        public void Dispose()
        {
        }

        private sealed class Logger(ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                lines.Enqueue($"{logLevel} {formatter(state, exception)} | {exception?.GetType().Name}");
        }
    }
}
