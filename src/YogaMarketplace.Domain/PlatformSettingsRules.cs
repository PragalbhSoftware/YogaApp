using System.Globalization;

namespace YogaMarketplace.Domain;

/// <summary>Money and window terms copied onto a booking when it is created.</summary>
public sealed record BookingTerms(
    decimal CommissionPercent,
    decimal ConvenienceFee,
    int CancelFreeWindowHours,
    LateCancelFeeType LateCancelFeeType,
    decimal LateCancelFeeValue);

/// <summary>Fields an admin may change in one save. Null leaves a field as it is; an empty text clears it.</summary>
public sealed record PlatformSettingsChanges(
    decimal? CommissionPercent = null,
    decimal? ConvenienceFee = null,
    int? CancelFreeWindowHours = null,
    int? RescheduleFreeWindowHours = null,
    LateCancelFeeType? LateCancelFeeType = null,
    decimal? LateCancelFeeValue = null,
    PayoutCycle? PayoutCycle = null,
    string? PolicyNote = null,
    string? BannerTitle = null,
    string? BannerSubtitle = null,
    string? BannerOffer = null);

public sealed record SettingChange(string Field, string? OldValue, string? NewValue);

public static class PlatformSettingsRules
{
    public const int MaxWindowHours = 168;
    public const decimal MaxFlatAmount = 10_000m;
    public const int PolicyNoteMax = 400;
    public const int BannerTitleMax = 80;
    public const int BannerSubtitleMax = 160;
    public const int BannerOfferMax = 60;

    public static BookingTerms TermsFor(PlatformSettings settings) => new(
        settings.CommissionPercent,
        settings.ConvenienceFee,
        settings.CancelFreeWindowHours,
        settings.LateCancelFeeType,
        settings.LateCancelFeeValue);

    /// <summary>
    /// Validates every field first, then applies <paramref name="changes"/>, so a bad value changes nothing.
    /// Returns only the fields whose value changed.
    /// </summary>
    public static IReadOnlyList<SettingChange> Apply(PlatformSettings settings, PlatformSettingsChanges changes)
    {
        var commission = changes.CommissionPercent is decimal c ? Percent(c, "Commission") : settings.CommissionPercent;
        var convenience = changes.ConvenienceFee is decimal f ? Flat(f, "Convenience fee") : settings.ConvenienceFee;
        var cancelHours = changes.CancelFreeWindowHours is int ch ? Hours(ch, "Late-cancel window") : settings.CancelFreeWindowHours;
        var rescheduleHours = changes.RescheduleFreeWindowHours is int rh ? Hours(rh, "Reschedule window") : settings.RescheduleFreeWindowHours;
        var feeType = changes.LateCancelFeeType ?? settings.LateCancelFeeType;
        var feeValue = changes.LateCancelFeeValue ?? settings.LateCancelFeeValue;
        if (changes.LateCancelFeeType is not null || changes.LateCancelFeeValue is not null)
            ValidateLateFee(feeType, feeValue);
        var cycle = changes.PayoutCycle ?? settings.PayoutCycle;
        if (!Enum.IsDefined(cycle))
            throw new DomainException("Payout cycle must be Weekly or Biweekly.");
        var note = changes.PolicyNote is null ? settings.PolicyNote : Text(changes.PolicyNote, PolicyNoteMax, "Policy note") ?? "";
        var title = changes.BannerTitle is null ? settings.BannerTitle : Text(changes.BannerTitle, BannerTitleMax, "Banner title");
        var subtitle = changes.BannerSubtitle is null ? settings.BannerSubtitle : Text(changes.BannerSubtitle, BannerSubtitleMax, "Banner subtitle");
        var offer = changes.BannerOffer is null ? settings.BannerOffer : Text(changes.BannerOffer, BannerOfferMax, "Banner offer");

        var log = new List<SettingChange>();
        Set(log, nameof(settings.CommissionPercent), settings.CommissionPercent, commission, v => settings.CommissionPercent = v);
        Set(log, nameof(settings.ConvenienceFee), settings.ConvenienceFee, convenience, v => settings.ConvenienceFee = v);
        Set(log, nameof(settings.CancelFreeWindowHours), settings.CancelFreeWindowHours, cancelHours, v => settings.CancelFreeWindowHours = v);
        Set(log, nameof(settings.RescheduleFreeWindowHours), settings.RescheduleFreeWindowHours, rescheduleHours, v => settings.RescheduleFreeWindowHours = v);
        Set(log, nameof(settings.LateCancelFeeType), settings.LateCancelFeeType, feeType, v => settings.LateCancelFeeType = v);
        Set(log, nameof(settings.LateCancelFeeValue), settings.LateCancelFeeValue, feeValue, v => settings.LateCancelFeeValue = v);
        Set(log, nameof(settings.PayoutCycle), settings.PayoutCycle, cycle, v => settings.PayoutCycle = v);
        Set(log, nameof(settings.PolicyNote), settings.PolicyNote, note, v => settings.PolicyNote = v);
        Set(log, nameof(settings.BannerTitle), settings.BannerTitle, title, v => settings.BannerTitle = v);
        Set(log, nameof(settings.BannerSubtitle), settings.BannerSubtitle, subtitle, v => settings.BannerSubtitle = v);
        Set(log, nameof(settings.BannerOffer), settings.BannerOffer, offer, v => settings.BannerOffer = v);
        return log;
    }

    private static void ValidateLateFee(LateCancelFeeType type, decimal value)
    {
        if (!Enum.IsDefined(type))
            throw new DomainException("Late-cancel fee type must be Percent or Flat.");
        if (type == LateCancelFeeType.Percent)
            Percent(value, "Late-cancel fee");
        else
            Flat(value, "Late-cancel fee");
    }

    private static void Set<T>(List<SettingChange> log, string field, T current, T next, Action<T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(current, next))
            return;
        log.Add(new SettingChange(field, Format(current), Format(next)));
        assign(next);
    }

    private static string? Format<T>(T value) => value switch
    {
        null => null,
        decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    private static decimal Percent(decimal value, string label)
    {
        if (value is < 0 or > 100)
            throw new DomainException($"{label} must be between 0 and 100 percent.");
        return TwoDecimals(value, label);
    }

    private static decimal Flat(decimal value, string label)
    {
        if (value < 0 || value > MaxFlatAmount)
            throw new DomainException($"{label} must be between 0 and {MaxFlatAmount:0}.");
        return TwoDecimals(value, label);
    }

    private static decimal TwoDecimals(decimal value, string label)
    {
        if (decimal.Round(value, 2) != value)
            throw new DomainException($"{label} supports at most 2 decimal places.");
        return value;
    }

    private static int Hours(int value, string label)
    {
        if (value is < 0 or > MaxWindowHours)
            throw new DomainException($"{label} must be between 0 and {MaxWindowHours} hours.");
        return value;
    }

    private static string? Text(string value, int max, string label)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > max)
            throw new DomainException($"{label} must be {max} characters or less.");
        return trimmed.Length == 0 ? null : trimmed;
    }
}

/// <summary>
/// Payout periods in India time. Weekly runs Monday to Sunday. Biweekly runs in 14-day blocks
/// counted from <see cref="BiweeklyAnchor"/>. Only payouts created before the current period starts are due.
/// </summary>
public static class PayoutCycleRules
{
    public static readonly DateOnly BiweeklyAnchor = new(2026, 1, 5);

    public static DateTimeOffset CurrentPeriodStart(PayoutCycle cycle, DateTimeOffset now)
    {
        var today = MumbaiClock.Today(now);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        if (cycle == PayoutCycle.Biweekly)
        {
            var weeks = (monday.DayNumber - BiweeklyAnchor.DayNumber) / 7;
            if (((weeks % 2) + 2) % 2 == 1)
                monday = monday.AddDays(-7);
        }
        return MumbaiClock.SessionStart(monday, TimeOnly.MinValue);
    }

    public static DateTimeOffset NextPeriodStart(PayoutCycle cycle, DateTimeOffset now) =>
        CurrentPeriodStart(cycle, now).AddDays(cycle == PayoutCycle.Biweekly ? 14 : 7);
}
