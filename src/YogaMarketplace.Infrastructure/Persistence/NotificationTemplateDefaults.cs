using YogaMarketplace.Domain;

namespace YogaMarketplace.Infrastructure.Persistence;

/// <summary>Default email text (no category) for each event. Seeded by migration, so every environment has them.</summary>
internal static class NotificationTemplateDefaults
{
    public static IEnumerable<NotificationTemplate> All() =>
    [
        Email("8f0c2a51-3b7e-4c1d-9a20-000000000001", NotificationEvents.BookingConfirmedCustomer,
            "Booked: {{serviceTitle}} with {{instructorName}} on {{sessionDate}}",
            """
            Hi {{customerName}},

            Your session is booked.

            Instructor: {{instructorName}}
            Session: {{serviceTitle}}
            When: {{sessionDate}} at {{sessionTime}}
            Booking reference: {{bookingRef}}

            You can see the details and cancel from My bookings.
            """),
        Email("8f0c2a51-3b7e-4c1d-9a20-000000000002", NotificationEvents.BookingConfirmedInstructor,
            "Booking confirmed: {{serviceTitle}} on {{sessionDate}}",
            """
            Hi {{instructorName}},

            You accepted the booking by {{customerName}} for {{serviceTitle}} on {{sessionDate}} at {{sessionTime}}.
            Booking reference: {{bookingRef}}
            """),
        Email("8f0c2a51-3b7e-4c1d-9a20-000000000008", NotificationEvents.BookingRequestedCustomer,
            "Request received: {{serviceTitle}} with {{instructorName}} on {{sessionDate}}",
            """
            Hi {{customerName}},

            We received your booking request. It is waiting for the instructor to accept it.

            Instructor: {{instructorName}}
            Session: {{serviceTitle}}
            When: {{sessionDate}} at {{sessionTime}}
            Booking reference: {{bookingRef}}

            We'll email you when the instructor accepts. If they decline, you get your payment or credit back.
            """),
        Email("8f0c2a51-3b7e-4c1d-9a20-000000000009", NotificationEvents.BookingRequestedInstructor,
            "New booking request: {{serviceTitle}} on {{sessionDate}}",
            """
            Hi {{instructorName}},

            {{customerName}} asked to book {{serviceTitle}} on {{sessionDate}} at {{sessionTime}}.
            Booking reference: {{bookingRef}}

            Open your bookings to accept or decline the request.
            """),
        Email("8f0c2a51-3b7e-4c1d-9a20-000000000003", NotificationEvents.BookingCancelledCustomer,
            "Cancelled: {{serviceTitle}} on {{sessionDate}}",
            """
            Hi {{customerName}},

            Your booking for {{serviceTitle}} with {{instructorName}} on {{sessionDate}} at {{sessionTime}} is cancelled.
            Refund: {{refund}}
            Booking reference: {{bookingRef}}
            """),
        Email("8f0c2a51-3b7e-4c1d-9a20-000000000004", NotificationEvents.BookingCancelledInstructor,
            "Booking cancelled: {{serviceTitle}} on {{sessionDate}}",
            """
            Hi {{instructorName}},

            The booking by {{customerName}} for {{serviceTitle}} on {{sessionDate}} at {{sessionTime}} is cancelled.
            Booking reference: {{bookingRef}}

            The slot is open again for other customers.
            """),
        Email("8f0c2a51-3b7e-4c1d-9a20-000000000005", NotificationEvents.BookingRefundedCustomer,
            "Refund for {{serviceTitle}} on {{sessionDate}}",
            """
            Hi {{customerName}},

            We refunded your booking for {{serviceTitle}} with {{instructorName}} on {{sessionDate}} at {{sessionTime}}.
            Refund: {{refund}}
            Booking reference: {{bookingRef}}
            """),
        Email("8f0c2a51-3b7e-4c1d-9a20-000000000006", NotificationEvents.BookingRefundedInstructor,
            "Booking refunded: {{serviceTitle}} on {{sessionDate}}",
            """
            Hi {{instructorName}},

            The booking by {{customerName}} for {{serviceTitle}} on {{sessionDate}} at {{sessionTime}} was refunded.
            Booking reference: {{bookingRef}}
            """),
        Email("8f0c2a51-3b7e-4c1d-9a20-000000000007", NotificationEvents.PayoutPaidInstructor,
            "Payout sent: {{amount}}",
            """
            Hi {{instructorName}},

            We sent your payout of {{amount}}.
            Payout reference: {{payoutRef}}
            """),
    ];

    private static NotificationTemplate Email(string id, string eventKey, string subject, string body) => new()
    {
        Id = Guid.Parse(id),
        EventKey = eventKey,
        Channel = NotificationChannel.Email,
        Subject = subject,
        Body = body.Replace("\r\n", "\n").Trim(),
        IsActive = true
    };
}
