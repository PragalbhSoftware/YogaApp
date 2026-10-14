using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace YogaMarketplace.Api.Logging;

/// <summary>
/// Where the JSON log file goes, how many days of files are kept and how big one file may grow before a new one
/// starts. An empty path writes no file.
/// </summary>
public class LogFileOptions
{
    public const string Section = "Logging:File";

    public string? Path { get; set; }
    public int RetainedDays { get; set; } = 14;
    public long FileSizeLimitBytes { get; set; } = 100L * 1024 * 1024;
}

public static class StructuredLogging
{
    /// <summary>
    /// One JSON object per line on the console and in a file that rolls daily. Levels come from the <c>Serilog</c>
    /// section. Error and Critical events are also queued for the <c>ErrorEvents</c> table. Other logger providers
    /// (tests add some) still receive every event.
    /// </summary>
    public static WebApplicationBuilder AddStructuredLogging(this WebApplicationBuilder builder)
    {
        var file = builder.Configuration.GetSection(LogFileOptions.Section).Get<LogFileOptions>() ?? new LogFileOptions();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ErrorEventQueue>();
        builder.Services.AddHostedService<ErrorEventWriter>();
        builder.Services.AddSerilog(
            (services, logger) =>
            {
                logger.ReadFrom.Configuration(builder.Configuration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(new RenderedCompactJsonFormatter())
                    .WriteTo.Sink(new ErrorEventSink(services.GetRequiredService<ErrorEventQueue>()), LogEventLevel.Error);
                if (!string.IsNullOrWhiteSpace(file.Path))
                {
                    logger.WriteTo.File(
                        new RenderedCompactJsonFormatter(),
                        file.Path,
                        rollingInterval: RollingInterval.Day,
                        fileSizeLimitBytes: file.FileSizeLimitBytes,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: null,
                        retainedFileTimeLimit: TimeSpan.FromDays(Math.Max(1, file.RetainedDays)),
                        shared: true);
                }
            },
            preserveStaticLogger: true,
            writeToProviders: true);
        return builder;
    }
}
