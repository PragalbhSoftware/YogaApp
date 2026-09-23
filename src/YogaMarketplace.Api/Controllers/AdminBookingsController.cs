using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[Route("api/admin/bookings")]
public class AdminBookingsController : AdminControllerBase
{
    private readonly IAdminBookingService _bookings;

    public AdminBookingsController(IAdminBookingService bookings)
    {
        _bookings = bookings;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminBookingResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] Guid? providerId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken) =>
        Ok(await _bookings.ListAsync(status, providerId, from, to, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminBookingResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _bookings.GetAsync(id, cancellationToken));
}
