namespace YogaMarketplace.Domain;

/// <summary>
/// Instructor edits to an availability slot. A blocked slot stays stored and is hidden from public booking
/// until the instructor unblocks it. A paid booking still occupies the slot, so it cannot be unblocked.
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

    public static void Unblock(AvailabilitySlot slot, bool hasOccupyingBooking, DateOnly today)
    {
        if (slot.Date < today)
            throw new DomainException("Past slots cannot be unblocked.");
        if (hasOccupyingBooking)
            throw new DomainException("That slot has a booking.", 409);

        slot.IsBlocked = false;
    }

    public static void Update(
        AvailabilitySlot slot,
        bool hasOccupyingBooking,
        DateOnly today,
        DateOnly date,
        TimeOnly start,
        TimeOnly end)
    {
        if (hasOccupyingBooking)
            throw new DomainException("That slot has a booking.", 409);
        if (slot.Date < today)
            throw new DomainException("Past slots cannot be edited.");
        if (date < today)
            throw new DomainException("Slots cannot be in the past.");
        if (end <= start)
            throw new DomainException("End time must be after the start time.");

        slot.Date = date;
        slot.StartTime = start;
        slot.EndTime = end;
    }

    public static void Delete(bool hasBooking)
    {
        if (hasBooking)
            throw new DomainException("That slot has a booking.", 409);
    }
}
