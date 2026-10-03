namespace YogaMarketplace.Domain;

/// <summary>
/// Admin block and unblock. A blocked user cannot sign in, their existing token stops working,
/// and a blocked provider leaves public browse. Existing bookings are left as they are.
/// Every change returns a <see cref="UserBlockEvent"/> for the audit trail.
/// </summary>
public static class UserBlocking
{
    public const int ReasonMin = 5;
    public const int ReasonMax = 300;

    public static UserBlockEvent Block(User user, Guid adminUserId, string? reason, DateTimeOffset now)
    {
        if (user.Role == UserRole.Admin)
            throw new DomainException("Admin accounts cannot be blocked.");
        if (user.IsBlocked)
            throw new DomainException("This user is already blocked.", 409);
        var trimmed = (reason ?? "").Trim();
        if (trimmed.Length is < ReasonMin or > ReasonMax)
            throw new DomainException($"Give a reason of {ReasonMin} to {ReasonMax} characters.");

        user.IsBlocked = true;
        user.BlockedAt = now;
        user.BlockedReason = trimmed;
        return NewEvent(user, adminUserId, UserBlockAction.Blocked, trimmed, now);
    }

    public static UserBlockEvent Unblock(User user, Guid adminUserId, string? reason, DateTimeOffset now)
    {
        if (!user.IsBlocked)
            throw new DomainException("This user is not blocked.", 409);
        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmed is { Length: > ReasonMax })
            throw new DomainException($"Reason must be {ReasonMax} characters or less.");

        user.IsBlocked = false;
        user.BlockedAt = null;
        user.BlockedReason = null;
        return NewEvent(user, adminUserId, UserBlockAction.Unblocked, trimmed, now);
    }

    public static void EnsureCanSignIn(User? user)
    {
        if (user is { IsBlocked: true })
            throw new DomainException("This account is suspended. Contact support.", 403);
    }

    private static UserBlockEvent NewEvent(User user, Guid adminUserId, UserBlockAction action, string? reason, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        AdminUserId = adminUserId,
        Action = action,
        Reason = reason,
        CreatedAt = now
    };
}
