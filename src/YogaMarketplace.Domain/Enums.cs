namespace YogaMarketplace.Domain;

public enum UserRole
{
    Customer,
    Provider,
    Admin
}

public enum Gender
{
    Female,
    Male,
    Other
}

public enum ProviderStatus
{
    Pending,
    Verified,
    Rejected
}

public enum SessionMode
{
    Home,
    Studio,
    Online
}

public enum BookingStatus
{
    PendingAccept,
    Upcoming,
    Declined,
    Completed,
    NoShow,
    Cancelled
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Refunded,
    PartiallyRefunded
}

public enum UserBlockAction
{
    Blocked,
    Unblocked
}

public enum RefreshTokenRevokeReason
{
    Rotated,
    Reused,
    SignedOut,
    Blocked
}

public enum CancelledBy
{
    Customer,
    Admin
}

/// <summary>
/// Unpaid Razorpay checkout. A booking row is created only after capture.
/// </summary>
public enum CheckoutStatus
{
    Open,
    Completed,
    /// <summary>Paid but refunded without a booking, for example a second trial with the same instructor.</summary>
    Refunded,
    /// <summary>Razorpay reported the payment failed. A later capture of the same order still books, or is refunded.</summary>
    Failed
}

public enum RefundStatus
{
    Pending,
    Processed,
    Failed
}

public enum WebhookEventStatus
{
    Received,
    Processed,
    /// <summary>Waiting for the payment it refers to; retried when that payment is captured.</summary>
    Pending,
    Failed
}

public enum PayoutStatus
{
    Pending,
    Exported,
    Paid
}

public enum LateCancelFeeType
{
    Percent,
    Flat
}

public enum PayoutCycle
{
    Weekly,
    Biweekly
}

public enum BackgroundJobStatus
{
    Pending,
    Running,
    Succeeded,
    Failed
}

public enum CheckoutKind
{
    Booking,
    Pack
}

public enum CreditEntryKind
{
    Purchase,
    Use,
    Return,
    Expire,
    /// <summary>Unused credits removed by an admin partial refund.</summary>
    Refund
}

/// <summary>What the instructor's gross is for a session paid with a credit.</summary>
public enum CreditPayBasis
{
    /// <summary>Pack price divided by its sessions.</summary>
    PackPerSession,
    /// <summary>The instructor's regular rate for the session mode.</summary>
    SessionPrice
}

/// <summary>What happens to the credit when the customer cancels a credit booking inside the late window.</summary>
public enum CreditLateCancel
{
    /// <summary>The credit is used and the instructor earns the late-cancel share of the frozen value.</summary>
    PayInstructorShare,
    /// <summary>The credit is used and nobody is paid.</summary>
    KeepCredit,
    /// <summary>The credit goes back to the customer.</summary>
    ReturnCredit
}

public enum CreditUnavailable
{
    NoCredits,
    AboveCap,
    ExpiresBeforeSession,
    Trial,
    Blocked
}

public enum CertificateStatus
{
    Pending,
    Approved,
    Rejected
}

public static class PaymentGateways
{
    public const string Razorpay = "razorpay";

    /// <summary>A credit booking with no fee to pay. Its payment row is ₹0 and never goes to Razorpay.</summary>
    public const string Credit = "credit";
}
