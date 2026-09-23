using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookings;

    public BookingsController(IBookingService bookings)
    {
        _bookings = bookings;
    }

    [HttpPost("orders")]
    public async Task<ActionResult<CheckoutOrderResponse>> CreateOrder(
        [FromBody] CreateBookingOrderRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _bookings.CreateOrderAsync(request, cancellationToken));
    }

    [HttpPost("confirm")]
    public async Task<ActionResult<BookingResponse>> Confirm(
        [FromBody] ConfirmBookingPaymentRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _bookings.ConfirmAsync(request, cancellationToken));
    }

    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> Mine(CancellationToken cancellationToken) =>
        Ok(await _bookings.ListMineAsync(cancellationToken));
}
