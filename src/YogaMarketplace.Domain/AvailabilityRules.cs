namespace YogaMarketplace.Domain;

/// <summary>
/// Instructor edits to an availability slot. A blocked slot stays stored and is hidden from public booking.
/// </summary>
public static class AvailabilityRules
{
    public static void Block(AvailabilitySlot slot, bool hasOccupyingBooking, DateOnly today)
    {
        if (slot.Date < today)
            throw new DomainException("Past slots cannot be blocked.");
        if (hasOccupyingBooking)
            throw new DomainException("That slot has a booking.", 409);

        slot.IsBlocked = true;
    }
}
