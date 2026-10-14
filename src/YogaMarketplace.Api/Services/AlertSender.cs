using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace YogaMarketplace.Api.Services;

/// <summary>Sends one operational alert email. 3.5.11 can swap the implementation for an INotificationService adapter.</summary>
public interface IAlertSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}

/// <summary>SMTP server for alerts. Set only in user-secrets or environment variables (<c>Alerts__Smtp__Host</c>, ...).</summary>
public class SmtpAlertOptions
{
    public const string Section = "Alerts:Smtp";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

/// <summary>Used when SMTP is not configured: alerts go nowhere, and errors still show under Admin › Errors.</summary>
public sealed class NoopAlertSender : IAlertSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class SmtpAlertSender : IAlertSender
{
    private readonly SmtpAlertOptions _options;

    public SmtpAlertSender(IOptions<SmtpAlertOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl };
        if (!string.IsNullOrWhiteSpace(_options.UserName))
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        using var message = new MailMessage(_options.From!, to, subject, body);
        await client.SendMailAsync(message, cancellationToken);
    }
}

public static class AlertSenders
{
    public static IServiceCollection AddAlertSender(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmtpAlertOptions>(configuration.GetSection(SmtpAlertOptions.Section));
        services.AddSingleton<IAlertSender>(sp => sp.GetRequiredService<IOptions<SmtpAlertOptions>>().Value.IsConfigured
            ? ActivatorUtilities.CreateInstance<SmtpAlertSender>(sp)
            : new NoopAlertSender());
        return services;
    }

    /// <summary>The one startup warning when error emails cannot go out, or null when they can.</summary>
    public static string? StartupWarning(bool smtpConfigured, bool emailAlertsEnabled) => (smtpConfigured, emailAlertsEnabled) switch
    {
        (false, _) => "Error alert emails are off: Alerts:Smtp:Host and Alerts:Smtp:From are not set, so alerts use a no-op sender. Errors still show under Admin › Errors.",
        (true, false) => "Error alert emails are off in admin settings. Errors still show under Admin › Errors.",
        _ => null
    };
}
