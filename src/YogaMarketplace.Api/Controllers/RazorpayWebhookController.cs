using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/webhooks/razorpay")]
public class RazorpayWebhookController : ControllerBase
{
    private readonly IRazorpayWebhookHandler _webhooks;

    public RazorpayWebhookController(IRazorpayWebhookHandler webhooks)
    {
        _webhooks = webhooks;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            body = await reader.ReadToEndAsync(cancellationToken);

        var signature = Request.Headers[RazorpayWire.SignatureHeader].ToString();
        var result = await _webhooks.HandleWebhookAsync(body, signature, cancellationToken);
        return Ok(new { received = true, booked = result.Booked, bookingId = result.BookingId });
    }
}
