namespace YogaMarketplace.Api.Services;

public interface IOtpSender
{
    Task SendAsync(string phone, string code, CancellationToken cancellationToken);
}

/// <summary>
/// Stub SMS sender. Replace with a real SMS or WhatsApp vendor before live OTP.
/// Logs keep only the last digits of the phone, and the code only in Development.
/// </summary>
public class LoggingOtpSender : IOtpSender
{
    private const int VisibleDigits = 4;

    private readonly ILogger<LoggingOtpSender> _logger;
    private readonly IHostEnvironment _environment;

    public LoggingOtpSender(ILogger<LoggingOtpSender> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public Task SendAsync(string phone, string code, CancellationToken cancellationToken)
    {
        var masked = MaskPhone(phone);
        if (_environment.IsDevelopment())
            _logger.LogInformation("OTP stub for {Phone}: {Code}", masked, code);
        else
            _logger.LogInformation("OTP stub sent to {Phone}.", masked);
        return Task.CompletedTask;
    }

    public static string MaskPhone(string phone) =>
        phone.Length <= VisibleDigits ? new string('*', phone.Length) : new string('*', phone.Length - VisibleDigits) + phone[^VisibleDigits..];
}
