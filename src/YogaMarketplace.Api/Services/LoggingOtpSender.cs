using YogaMarketplace.Api.Security;

namespace YogaMarketplace.Api.Services;

public interface IOtpSender
{
    Task SendAsync(string phone, string code, CancellationToken cancellationToken);
}

/// <summary>
/// Stub SMS sender. Replace with a real SMS or WhatsApp vendor before live OTP.
/// Logs keep only the last digits of the phone and never the code; locally use <c>Otp:ExposeCode</c> or a fixed code.
/// </summary>
public class LoggingOtpSender : IOtpSender
{
    private readonly ILogger<LoggingOtpSender> _logger;

    public LoggingOtpSender(ILogger<LoggingOtpSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string phone, string code, CancellationToken cancellationToken)
    {
        _logger.LogInformation("OTP stub sent to {Phone}.", LogMask.Phone(phone));
        return Task.CompletedTask;
    }
}
