using System.Globalization;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

/// <summary>
/// Queues the customer and instructor emails for a booking event. Call before the save that records the event, so the
/// emails commit or roll back with it. The booking's Provider, Service and Slot must be loaded.
/// </summary>
public class BookingNotifications
{
    private const string UnnamedCustomer = "Customer";

    private readonly YogaDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IPlatformSettingsService _settings;

    public BookingNotifications(YogaDbContext db, INotificationService notifications, IPlatformSettingsService settings)
    {
        _db = db;
        _notifications = notifications;
        _settings = settings;
    }

    /// <summary>A new booking still waits for the instructor, so it gets the request wording; once accepted it is confirmed.</summary>
    public Task BookedAsync(Booking booking, CancellationToken cancellationToken) => booking.Status == BookingStatus.PendingAccept
        ? QueueAsync(booking, NotificationEvents.BookingRequestedCustomer, NotificationEvents.BookingRequestedInstructor, null, cancellationToken)
        : QueueAsync(booking, NotificationEvents.BookingConfirmedCustomer, NotificationEvents.BookingConfirmedInstructor, null, cancellationToken);

    /// <summary>Customer or admin cancel. <paramref name="refund"/> is the money going back; a returned credit is named too.</summary>
    public async Task CancelledAsync(Booking booking, decimal refund, bool creditReturned, CancellationToken cancellationToken) =>
        await QueueAsync(booking, NotificationEvents.BookingCancelledCustomer, NotificationEvents.BookingCancelledInstructor,
            await RefundTextAsync(booking, refund, creditReturned, cancellationToken), cancellationToken);

    /// <summary>Instructor decline, or a refund made in the Razorpay dashboard.</summary>
    public async Task RefundedAsync(Booking booking, decimal refund, bool creditReturned, CancellationToken cancellationToken) =>
        await QueueAsync(booking, NotificationEvents.BookingRefundedCustomer, NotificationEvents.BookingRefundedInstructor,
            await RefundTextAsync(booking, refund, creditReturned, cancellationToken), cancellationToken);

    private async Task QueueAsync(Booking booking, string customerEvent, string instructorEvent, string? refund, CancellationToken cancellationToken)
    {
        var provider = booking.Provider ?? throw new InvalidOperationException("Provider was not loaded.");
        var service = booking.Service ?? throw new InvalidOperationException("Service was not loaded.");
        var slot = booking.Slot ?? throw new InvalidOperationException("Slot was not loaded.");
        var users = await _db.Users.AsNoTracking()
            .Where(u => u.Id == booking.CustomerId || u.Id == provider.UserId)
            .Select(u => new { u.Id, u.Name, u.Email, u.Phone })
            .ToListAsync(cancellationToken);
        var customer = users.Single(u => u.Id == booking.CustomerId);
        var instructor = users.Single(u => u.Id == provider.UserId);

        var data = new Dictionary<string, string>
        {
            ["customerName"] = string.IsNullOrWhiteSpace(customer.Name) ? UnnamedCustomer : customer.Name,
            ["instructorName"] = provider.DisplayName,
            ["serviceTitle"] = service.Title,
            ["sessionDate"] = slot.Date.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture),
            ["sessionTime"] = slot.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            ["bookingRef"] = booking.Id.ToString("N")[..8].ToUpperInvariant()
        };
        if (refund is not null)
            data["refund"] = refund;

        await _notifications.EnqueueAsync(customerEvent, new NotificationRecipient(customer.Id, customer.Email, customer.Phone), data, service.CategoryId, cancellationToken);
        await _notifications.EnqueueAsync(instructorEvent, new NotificationRecipient(instructor.Id, instructor.Email, instructor.Phone), data, service.CategoryId, cancellationToken);
    }

    private async Task<string> RefundTextAsync(Booking booking, decimal refund, bool creditReturned, CancellationToken cancellationToken) =>
        RefundText((await _settings.GetAsync(cancellationToken)).Currency, refund, booking.CreditBatchId is not null, creditReturned);

    /// <summary>The value after "Refund:". A credit booking always says what happened to the credit, even with no money back.</summary>
    public static string RefundText(string currency, decimal refund, bool paidWithCredit, bool creditReturned)
    {
        var money = $"{currency} {refund.ToString("0.00", CultureInfo.InvariantCulture)}";
        if (!paidWithCredit)
            return money;
        return (creditReturned, refund > 0) switch
        {
            (true, true) => $"{money}, and 1 credit back in your pack",
            (true, false) => "1 credit back in your pack",
            (false, true) => $"{money}. The credit for this session stays used",
            (false, false) => "None. The credit for this session stays used"
        };
    }
}
