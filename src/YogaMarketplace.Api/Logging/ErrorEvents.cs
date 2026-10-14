using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Serilog.Core;
using Serilog.Events;
using YogaMarketplace.Api.Middleware;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Logging;

/// <summary>One logged Error or Critical, already masked and cut to the column sizes.</summary>
public sealed record ErrorOccurrence(
    string ErrorType,
    string Level,
    string? Source,
    string Message,
    string? CorrelationId,
    DateTimeOffset At);

/// <summary>Hands errors from the logging pipeline to <see cref="ErrorEventWriter"/>. When it is full, new errors are dropped.</summary>
public class ErrorEventQueue
{
    private const int Capacity = 1000;

    private readonly Channel<ErrorOccurrence> _channel = Channel.CreateBounded<ErrorOccurrence>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public bool TryWrite(ErrorOccurrence occurrence) => _channel.Writer.TryWrite(occurrence);

    public ChannelReader<ErrorOccurrence> Reader => _channel.Reader;
}

/// <summary>
/// Turns Error and Critical log events into <see cref="ErrorOccurrence"/>s. The error type is the exception type and
/// logger name, or the logger name and message template when no exception was logged.
/// </summary>
public class ErrorEventSink : ILogEventSink
{
    private readonly ErrorEventQueue _queue;

    public ErrorEventSink(ErrorEventQueue queue)
    {
        _queue = queue;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Error || ErrorEventWriter.IsWriting)
            return;
        _queue.TryWrite(ToOccurrence(logEvent));
    }

    internal static ErrorOccurrence ToOccurrence(LogEvent logEvent)
    {
        var source = Text(logEvent, "SourceContext");
        var exception = logEvent.Exception;
        var type = exception is null
            ? $"{source ?? "App"}: {logEvent.MessageTemplate.Text}"
            : $"{exception.GetType().FullName}: {source ?? "App"}";
        var message = exception is null ? logEvent.RenderMessage() : $"{logEvent.RenderMessage()} {exception.Message}";
        return new ErrorOccurrence(
            Cut(type, ErrorEvent.ErrorTypeMax)!,
            logEvent.Level == LogEventLevel.Fatal ? "Critical" : "Error",
            Cut(source, ErrorEvent.SourceMax),
            Cut(LogMask.Text(message), ErrorEvent.MessageMax)!,
            Cut(Text(logEvent, CorrelationIdMiddleware.LogProperty), ErrorEvent.CorrelationIdMax),
            logEvent.Timestamp.ToUniversalTime());
    }

    private static string? Text(LogEvent logEvent, string property) =>
        logEvent.Properties.TryGetValue(property, out var value) && value is ScalarValue { Value: { } scalar }
            ? scalar.ToString()
            : null;

    private static string? Cut(string? value, int max) => value is not null && value.Length > max ? value[..max] : value;
}

/// <summary>
/// Saves queued errors, one row per error type. Its own database errors are not queued again, so a database outage
/// cannot loop; they are logged as warnings instead.
/// </summary>
public class ErrorEventWriter : BackgroundService
{
    private static readonly AsyncLocal<bool> Writing = new();

    private readonly ErrorEventQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ErrorEventWriter> _logger;

    public ErrorEventWriter(ErrorEventQueue queue, IServiceScopeFactory scopes, ILogger<ErrorEventWriter> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    public static bool IsWriting => Writing.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var occurrence in _queue.Reader.ReadAllAsync(stoppingToken))
            await WriteAsync(occurrence, stoppingToken);
    }

    public async Task WriteAsync(ErrorOccurrence occurrence, CancellationToken cancellationToken)
    {
        Writing.Value = true;
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            try
            {
                await UpsertAsync(db, occurrence, cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Another instance inserted the same error type first; count this occurrence on its row.
                db.ChangeTracker.Clear();
                await UpsertAsync(db, occurrence, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not record error event {ErrorType}.", occurrence.ErrorType);
        }
        finally
        {
            Writing.Value = false;
        }
    }

    private static async Task UpsertAsync(YogaDbContext db, ErrorOccurrence o, CancellationToken cancellationToken)
    {
        var counted = await db.ErrorEvents
            .Where(e => e.ErrorType == o.ErrorType && e.AcknowledgedAt == null)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(e => e.Count, e => e.Count + 1)
                    .SetProperty(e => e.Level, o.Level)
                    .SetProperty(e => e.Source, o.Source)
                    .SetProperty(e => e.LastMessage, o.Message)
                    .SetProperty(e => e.LastCorrelationId, o.CorrelationId)
                    .SetProperty(e => e.LastSeenAt, o.At),
                cancellationToken);
        if (counted > 0)
            return;

        var reopened = await db.ErrorEvents
            .Where(e => e.ErrorType == o.ErrorType && e.AcknowledgedAt != null)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(e => e.Count, 1)
                    .SetProperty(e => e.Level, o.Level)
                    .SetProperty(e => e.Source, o.Source)
                    .SetProperty(e => e.LastMessage, o.Message)
                    .SetProperty(e => e.LastCorrelationId, o.CorrelationId)
                    .SetProperty(e => e.FirstSeenAt, o.At)
                    .SetProperty(e => e.LastSeenAt, o.At)
                    .SetProperty(e => e.AcknowledgedAt, (DateTimeOffset?)null)
                    .SetProperty(e => e.AcknowledgedByUserId, (Guid?)null),
                cancellationToken);
        if (reopened > 0)
            return;

        db.ErrorEvents.Add(new ErrorEvent
        {
            Id = Guid.NewGuid(),
            ErrorType = o.ErrorType,
            Level = o.Level,
            Source = o.Source,
            LastMessage = o.Message,
            LastCorrelationId = o.CorrelationId,
            Count = 1,
            FirstSeenAt = o.At,
            LastSeenAt = o.At
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
