namespace YogaMarketplace.Api.Services;

public interface IOtpSender
{
    Task SendAsync(string phone, string code, CancellationToken cancellationToken);
}

/// <summary>
/// Stub SMS sender. Replace with a real SMS or WhatsApp vendor before live OTP.
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
        _logger.LogInformation("OTP stub for {Phone}: {Code}", phone, code);
        return Task.CompletedTask;
    }
}
