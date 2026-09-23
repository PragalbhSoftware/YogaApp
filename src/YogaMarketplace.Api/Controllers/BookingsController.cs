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
    private readonly IBookingHandshake _handshake;

    public BookingsController(IBookingService bookings, IBookingHandshake handshake)
    {
        _bookings = bookings;
        _handshake = handshake;
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

    [HttpGet("instructor")]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> ForInstructor(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _handshake.ListForInstructorAsync(status, cancellationToken));

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<BookingResponse>> Accept(Guid id, CancellationToken cancellationToken) =>
        Ok(await _handshake.AcceptAsync(id, cancellationToken));

    [HttpPost("{id:guid}/decline")]
    public async Task<ActionResult<BookingResponse>> Decline(Guid id, CancellationToken cancellationToken) =>
        Ok(await _handshake.DeclineAsync(id, cancellationToken));

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<BookingResponse>> Complete(Guid id, CancellationToken cancellationToken) =>
        Ok(await _handshake.CompleteAsync(id, cancellationToken));

    [HttpPost("{id:guid}/reviews")]
    public async Task<ActionResult<ReviewResponse>> Review(
        Guid id,
        [FromBody] CreateReviewRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        var review = await _handshake.CreateReviewAsync(id, request, cancellationToken);
        return Created($"/api/bookings/{id}/reviews/{review.Id}", review);
    }
}
