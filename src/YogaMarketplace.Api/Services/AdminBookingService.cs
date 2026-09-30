using System.Globalization;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
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

    Task<AdminBookingResponse> CancelAsync(Guid id, AdminCancelBookingRequest request, CancellationToken cancellationToken);
}

public class AdminBookingService : IAdminBookingService
{
    private readonly YogaDbContext _db;
    private readonly IRazorpayClient _razorpay;
    private readonly ICurrentUser _current;
    private readonly ILogger<AdminBookingService> _logger;

    public AdminBookingService(
        YogaDbContext db,
        IRazorpayClient razorpay,
        ICurrentUser current,
        ILogger<AdminBookingService> logger)
    {
        _db = db;
        _razorpay = razorpay;
        _current = current;
        _logger = logger;
    }

    public async Task<AdminBookingResponse> CancelAsync(
        Guid id,
        AdminCancelBookingRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Provider)
            .Include(b => b.Service)
            .Include(b => b.Slot)
            .Include(b => b.Payment)
            .Include(b => b.Review)
            .Include(b => b.Payout)
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new DomainException("Booking not found.", 404);

        if (booking.Status is not (BookingStatus.PendingAccept or BookingStatus.Upcoming))
            throw new DomainException("Only a pending or upcoming booking can be cancelled.", 409);
        var payment = booking.Payment;
        if (payment is null || payment.Status != PaymentStatus.Paid || string.IsNullOrWhiteSpace(payment.GatewayPaymentId))
            throw new DomainException("This booking has no captured payment to refund.", 409);

        BookingRules.AdminCancel(booking, request.Reason, DateTimeOffset.UtcNow);
        await PaymentRefunds.RefundAsync(_razorpay, booking, payment, payment.Amount, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {AdminId} force-cancelled booking {BookingId} and refunded {Amount} on payment {PaymentId}.",
            _current.UserId,
            booking.Id,
            payment.RefundedAmount,
            payment.GatewayPaymentId);
        return ToResponse(booking, await CurrencyAsync(cancellationToken));
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
            booking.CreatedAt,
            booking.Payment?.RefundedAmount,
            booking.LateCancelFee,
            booking.CancelledBy?.ToString(),
            booking.CancelReason);
    }
}
