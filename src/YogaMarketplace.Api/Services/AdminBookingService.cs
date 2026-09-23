using System.Globalization;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public interface IAdminBookingService
{
    Task<IReadOnlyList<AdminBookingResponse>> ListAsync(
        string? status,
        Guid? providerId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken);

    Task<AdminBookingResponse> GetAsync(Guid id, CancellationToken cancellationToken);
}

public class AdminBookingService : IAdminBookingService
{
    private readonly YogaDbContext _db;

    public AdminBookingService(YogaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AdminBookingResponse>> ListAsync(
        string? status,
        Guid? providerId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (from is DateOnly start && to is DateOnly end && end < start)
            throw new DomainException("The end date is before the start date.");

        var parsed = AdminQuery.ParseOptional<BookingStatus>(status, "Unknown booking status.");
        var query = Details();
        if (parsed is BookingStatus bookingStatus)
            query = query.Where(b => b.Status == bookingStatus);
        if (providerId is Guid id && id != Guid.Empty)
            query = query.Where(b => b.ProviderId == id);
        if (from is DateOnly fromDate)
            query = query.Where(b => b.Slot!.Date >= fromDate);
        if (to is DateOnly toDate)
            query = query.Where(b => b.Slot!.Date <= toDate);

        var currency = await CurrencyAsync(cancellationToken);
        var bookings = await query.ToListAsync(cancellationToken);
        return bookings
            .OrderByDescending(b => b.CreatedAt)
            .Take(AdminQuery.PageSize)
            .Select(b => ToResponse(b, currency))
            .ToList();
    }

    public async Task<AdminBookingResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var booking = await Details().SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new DomainException("Booking not found.", 404);
        return ToResponse(booking, await CurrencyAsync(cancellationToken));
    }

    private IQueryable<Booking> Details() =>
        _db.Bookings.AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.Provider)
            .Include(b => b.Service)
            .Include(b => b.Slot)
            .Include(b => b.Payment)
            .Include(b => b.Review)
            .Include(b => b.Payout);

    private Task<string> CurrencyAsync(CancellationToken cancellationToken) =>
        _db.Policies.AsNoTracking().Select(p => p.Currency).SingleAsync(cancellationToken);

    private static AdminBookingResponse ToResponse(Booking booking, string currency)
    {
        var customer = booking.Customer ?? throw new InvalidOperationException("Customer was not loaded.");
        var provider = booking.Provider ?? throw new InvalidOperationException("Provider was not loaded.");
        var service = booking.Service ?? throw new InvalidOperationException("Service was not loaded.");
        var slot = booking.Slot ?? throw new InvalidOperationException("Slot was not loaded.");
        return new AdminBookingResponse(
            booking.Id,
            customer.Id,
            customer.Name,
            customer.Phone,
            provider.Id,
            provider.DisplayName,
            service.Id,
            service.Title,
            slot.Id,
            booking.Mode.ToString(),
            booking.Status.ToString(),
            booking.Amount,
            currency,
            slot.Date,
            slot.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            slot.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            booking.HomeAddress,
            booking.Landmark,
            booking.MeetLinkSnapshot,
            booking.StudioAddressSnapshot,
            booking.Payment?.Status.ToString(),
            booking.Payment?.Id,
            booking.Payment?.GatewayOrderId,
            booking.Payment?.GatewayPaymentId,
            booking.Review?.Rating,
            booking.Payout?.NetAmount,
            booking.Payout?.Status.ToString(),
            booking.CreatedAt);
    }
}
