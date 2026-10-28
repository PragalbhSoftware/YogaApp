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
    string? BannerOffer = null,
    bool? CreditConvenienceFee = null,
    CreditPayBasis? CreditPayBasis = null,
    CreditLateCancel? CreditLateCancel = null,
    bool? CreditBookingStandsAfterExpiry = null,
    bool? PackRefundsEnabled = null,
    decimal? PackReferenceSessionPrice = null,
    decimal? PackRefundFee = null,
    TimeOnly? ReconciliationTime = null,
    int? WebhookStuckMinutes = null,
    int? RefundPendingHours = null,
    int? PolicyVersion = null,
    int? WebhookPayloadRetentionDays = null,
    bool? ReconciliationEnabled = null,
    bool? CreditExpiryEnabled = null,
    TimeOnly? CreditExpiryTime = null,
    bool? WebhookCleanupEnabled = null,
    TimeOnly? WebhookCleanupTime = null,
    bool? EmailAlertsEnabled = null,
    string? ErrorAlertEmail = null,
    int? ErrorAlertThrottleMinutes = null,
    int? ErrorAlertCheckMinutes = null,
    int? ErrorEventRetentionDays = null,
    bool? EmailNotificationsEnabled = null,
    int? NotificationMaxAttempts = null,
    int? OtpMaxSendsPerPhone = null,
    int? OtpMaxWrongCodesPerPhone = null,
    int? OtpLockMinutes = null,
    string? TimeZoneId = null,
    decimal? NearestCityMaxKm = null);

/// <summary>When the daily reconciliation runs and how long a webhook or refund may wait before it is reported.</summary>
public sealed record ReconciliationTerms(TimeOnly RunAt, int WebhookStuckMinutes, int RefundPendingHours);

/// <summary>Whether error emails go out, to whom, at most how often per error type, and how long errors are kept.</summary>
public sealed record ErrorAlertTerms(bool EmailEnabled, string? Email, int ThrottleMinutes, int RetentionDays);

/// <summary>Schedules of the recurring jobs.</summary>
public sealed record RecurringJobTerms(JobSchedule Reconciliation, JobSchedule CreditExpiry, JobSchedule WebhookCleanup, JobSchedule ErrorAlerts);

public sealed record SettingChange(string Field, string? OldValue, string? NewValue);

public static class PlatformSettingsRules
{
    public const int MaxWindowHours = 168;
    public const decimal MaxFlatAmount = 10_000m;
    public const int PolicyNoteMax = 400;
    public const int BannerTitleMax = 80;
    public const int BannerSubtitleMax = 160;
    public const int BannerOfferMax = 60;
    public const decimal DefaultPackReferenceSessionPrice = 799m;
    public static readonly TimeOnly DefaultReconciliationTime = new(6, 0);
    public const int DefaultWebhookStuckMinutes = 60;
    public const int DefaultRefundPendingHours = 48;
    public const int MaxWebhookStuckMinutes = 7 * 24 * 60;
    public const int MaxRefundPendingHours = 30 * 24;
    public const int DefaultWebhookPayloadRetentionDays = 90;
    public const int MaxWebhookPayloadRetentionDays = 3650;
    public static readonly TimeOnly DefaultDailyJobTime = TimeOnly.MinValue;
    public const int DefaultErrorAlertThrottleMinutes = 30;
    public const int MaxErrorAlertThrottleMinutes = 7 * 24 * 60;
    public const int DefaultErrorAlertCheckMinutes = 5;
    public const int MaxErrorAlertCheckMinutes = 24 * 60;
    public const int DefaultErrorEventRetentionDays = 30;
    public const int MaxErrorEventRetentionDays = 3650;
    public const int ErrorAlertEmailMax = 254;
    public const int DefaultNotificationMaxAttempts = 5;
    public const int MaxNotificationMaxAttempts = 10;
    public const int DefaultOtpMaxSendsPerPhone = 5;
    public const int MaxOtpMaxSendsPerPhone = 20;
    public const int DefaultOtpMaxWrongCodesPerPhone = 10;
    public const int MaxOtpMaxWrongCodesPerPhone = 50;
    public const int DefaultOtpLockMinutes = 30;
    public const int MaxOtpLockMinutes = 24 * 60;
    public const decimal DefaultNearestCityMaxKm = 100m;
    public const decimal MaxNearestCityMaxKm = 500m;

    public static NotificationTerms NotificationTermsFor(PlatformSettings settings) => new(
        settings.EmailNotificationsEnabled,
        settings.NotificationMaxAttempts);

    public static OtpLimitTerms OtpLimitTermsFor(PlatformSettings settings) => new(
        settings.OtpMaxSendsPerPhone,
        settings.OtpMaxWrongCodesPerPhone,
        settings.OtpLockMinutes);

    public static ErrorAlertTerms ErrorAlertTermsFor(PlatformSettings settings) => new(
        settings.EmailAlertsEnabled,
        settings.ErrorAlertEmail,
        settings.ErrorAlertThrottleMinutes,
        settings.ErrorEventRetentionDays);

    public static ReconciliationTerms ReconciliationTermsFor(PlatformSettings settings) => new(
        settings.ReconciliationTime,
        settings.WebhookStuckMinutes,
        settings.RefundPendingHours);

    public static RecurringJobTerms RecurringJobTermsFor(PlatformSettings settings) => new(
        DailyIf(settings.ReconciliationEnabled, settings.ReconciliationTime),
        DailyIf(settings.CreditExpiryEnabled, settings.CreditExpiryTime),
        DailyIf(settings.WebhookCleanupEnabled, settings.WebhookCleanupTime),
        JobSchedule.Every(settings.ErrorAlertCheckMinutes));

    private static JobSchedule DailyIf(bool enabled, TimeOnly at) => enabled ? JobSchedule.Daily(at) : JobSchedule.Off;

    public static BookingTerms TermsFor(PlatformSettings settings) => new(
        settings.CommissionPercent,
        settings.ConvenienceFee,
        settings.CancelFreeWindowHours,
        settings.LateCancelFeeType,
        settings.LateCancelFeeValue);

    public static CreditTerms CreditTermsFor(PlatformSettings settings) => new(
        settings.CreditConvenienceFee,
        settings.CreditPayBasis,
        settings.CreditLateCancel,
        settings.CreditBookingStandsAfterExpiry,
        settings.PackRefundsEnabled,
        settings.PackReferenceSessionPrice,
        settings.PackRefundFee);

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
        var creditFee = changes.CreditConvenienceFee ?? settings.CreditConvenienceFee;
        var payBasis = changes.CreditPayBasis ?? settings.CreditPayBasis;
        if (!Enum.IsDefined(payBasis))
            throw new DomainException("Credit pay basis must be PackPerSession or SessionPrice.");
        var lateCancel = changes.CreditLateCancel ?? settings.CreditLateCancel;
        if (!Enum.IsDefined(lateCancel))
            throw new DomainException("Credit late-cancel rule must be PayInstructorShare, KeepCredit or ReturnCredit.");
        var standsAfterExpiry = changes.CreditBookingStandsAfterExpiry ?? settings.CreditBookingStandsAfterExpiry;
        var refunds = changes.PackRefundsEnabled ?? settings.PackRefundsEnabled;
        var referencePrice = changes.PackReferenceSessionPrice is decimal rp ? Flat(rp, "Reference session price") : settings.PackReferenceSessionPrice;
        var refundFee = changes.PackRefundFee is decimal rf ? Flat(rf, "Pack refund fee") : settings.PackRefundFee;
        var reconcileAt = changes.ReconciliationTime is TimeOnly rt ? WholeMinute(rt, "Reconciliation time") : settings.ReconciliationTime;
        var reconcileOn = changes.ReconciliationEnabled ?? settings.ReconciliationEnabled;
        var expiryOn = changes.CreditExpiryEnabled ?? settings.CreditExpiryEnabled;
        var expiryAt = changes.CreditExpiryTime is TimeOnly et ? WholeMinute(et, "Credit expiry time") : settings.CreditExpiryTime;
        var cleanupOn = changes.WebhookCleanupEnabled ?? settings.WebhookCleanupEnabled;
        var cleanupAt = changes.WebhookCleanupTime is TimeOnly wt ? WholeMinute(wt, "Webhook cleanup time") : settings.WebhookCleanupTime;
        var stuckMinutes = changes.WebhookStuckMinutes is int sm ? Between(sm, MaxWebhookStuckMinutes, "Stuck webhook threshold", "minutes") : settings.WebhookStuckMinutes;
        var pendingHours = changes.RefundPendingHours is int ph ? Between(ph, MaxRefundPendingHours, "Pending refund threshold", "hours") : settings.RefundPendingHours;
        var policyVersion = changes.PolicyVersion is int pv ? NotLower(pv, settings.PolicyVersion) : settings.PolicyVersion;
        var retentionDays = changes.WebhookPayloadRetentionDays is int rd ? Between(rd, MaxWebhookPayloadRetentionDays, "Webhook payload retention", "days") : settings.WebhookPayloadRetentionDays;
        var alertsOn = changes.EmailAlertsEnabled ?? settings.EmailAlertsEnabled;
        var alertEmail = changes.ErrorAlertEmail is null ? settings.ErrorAlertEmail : Email(changes.ErrorAlertEmail);
        if (alertsOn && alertEmail is null)
            throw new DomainException("Add the admin alert email before turning email alerts on.");
        var throttleMinutes = changes.ErrorAlertThrottleMinutes is int tm ? Between(tm, MaxErrorAlertThrottleMinutes, "Alert throttle", "minutes") : settings.ErrorAlertThrottleMinutes;
        var checkMinutes = changes.ErrorAlertCheckMinutes is int cm ? Between(cm, MaxErrorAlertCheckMinutes, "Error check interval", "minutes") : settings.ErrorAlertCheckMinutes;
        var errorRetentionDays = changes.ErrorEventRetentionDays is int ed ? Between(ed, MaxErrorEventRetentionDays, "Error retention", "days") : settings.ErrorEventRetentionDays;
        var notificationsOn = changes.EmailNotificationsEnabled ?? settings.EmailNotificationsEnabled;
        var notificationAttempts = changes.NotificationMaxAttempts is int na ? Between(na, MaxNotificationMaxAttempts, "Notification attempts", "tries") : settings.NotificationMaxAttempts;
        var otpSends = changes.OtpMaxSendsPerPhone is int os ? Between(os, MaxOtpMaxSendsPerPhone, "Codes per phone", "codes") : settings.OtpMaxSendsPerPhone;
        var otpWrong = changes.OtpMaxWrongCodesPerPhone is int ow ? Between(ow, MaxOtpMaxWrongCodesPerPhone, "Wrong codes per phone", "tries") : settings.OtpMaxWrongCodesPerPhone;
        var otpLock = changes.OtpLockMinutes is int ol ? Between(ol, MaxOtpLockMinutes, "Phone lock", "minutes") : settings.OtpLockMinutes;
        var timeZoneId = changes.TimeZoneId is null ? settings.TimeZoneId : MarketClock.Normalize(changes.TimeZoneId);
        var nearestKm = changes.NearestCityMaxKm is decimal km ? Kilometres(km) : settings.NearestCityMaxKm;

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
        Set(log, nameof(settings.CreditConvenienceFee), settings.CreditConvenienceFee, creditFee, v => settings.CreditConvenienceFee = v);
        Set(log, nameof(settings.CreditPayBasis), settings.CreditPayBasis, payBasis, v => settings.CreditPayBasis = v);
        Set(log, nameof(settings.CreditLateCancel), settings.CreditLateCancel, lateCancel, v => settings.CreditLateCancel = v);
        Set(log, nameof(settings.CreditBookingStandsAfterExpiry), settings.CreditBookingStandsAfterExpiry, standsAfterExpiry, v => settings.CreditBookingStandsAfterExpiry = v);
        Set(log, nameof(settings.PackRefundsEnabled), settings.PackRefundsEnabled, refunds, v => settings.PackRefundsEnabled = v);
        Set(log, nameof(settings.PackReferenceSessionPrice), settings.PackReferenceSessionPrice, referencePrice, v => settings.PackReferenceSessionPrice = v);
        Set(log, nameof(settings.PackRefundFee), settings.PackRefundFee, refundFee, v => settings.PackRefundFee = v);
        Set(log, nameof(settings.ReconciliationTime), settings.ReconciliationTime, reconcileAt, v => settings.ReconciliationTime = v);
        Set(log, nameof(settings.WebhookStuckMinutes), settings.WebhookStuckMinutes, stuckMinutes, v => settings.WebhookStuckMinutes = v);
        Set(log, nameof(settings.RefundPendingHours), settings.RefundPendingHours, pendingHours, v => settings.RefundPendingHours = v);
        Set(log, nameof(settings.PolicyVersion), settings.PolicyVersion, policyVersion, v => settings.PolicyVersion = v);
        Set(log, nameof(settings.WebhookPayloadRetentionDays), settings.WebhookPayloadRetentionDays, retentionDays, v => settings.WebhookPayloadRetentionDays = v);
        Set(log, nameof(settings.ReconciliationEnabled), settings.ReconciliationEnabled, reconcileOn, v => settings.ReconciliationEnabled = v);
        Set(log, nameof(settings.CreditExpiryEnabled), settings.CreditExpiryEnabled, expiryOn, v => settings.CreditExpiryEnabled = v);
        Set(log, nameof(settings.CreditExpiryTime), settings.CreditExpiryTime, expiryAt, v => settings.CreditExpiryTime = v);
        Set(log, nameof(settings.WebhookCleanupEnabled), settings.WebhookCleanupEnabled, cleanupOn, v => settings.WebhookCleanupEnabled = v);
        Set(log, nameof(settings.WebhookCleanupTime), settings.WebhookCleanupTime, cleanupAt, v => settings.WebhookCleanupTime = v);
        Set(log, nameof(settings.EmailAlertsEnabled), settings.EmailAlertsEnabled, alertsOn, v => settings.EmailAlertsEnabled = v);
        Set(log, nameof(settings.ErrorAlertEmail), settings.ErrorAlertEmail, alertEmail, v => settings.ErrorAlertEmail = v);
        Set(log, nameof(settings.ErrorAlertThrottleMinutes), settings.ErrorAlertThrottleMinutes, throttleMinutes, v => settings.ErrorAlertThrottleMinutes = v);
        Set(log, nameof(settings.ErrorAlertCheckMinutes), settings.ErrorAlertCheckMinutes, checkMinutes, v => settings.ErrorAlertCheckMinutes = v);
        Set(log, nameof(settings.ErrorEventRetentionDays), settings.ErrorEventRetentionDays, errorRetentionDays, v => settings.ErrorEventRetentionDays = v);
        Set(log, nameof(settings.EmailNotificationsEnabled), settings.EmailNotificationsEnabled, notificationsOn, v => settings.EmailNotificationsEnabled = v);
        Set(log, nameof(settings.NotificationMaxAttempts), settings.NotificationMaxAttempts, notificationAttempts, v => settings.NotificationMaxAttempts = v);
        Set(log, nameof(settings.OtpMaxSendsPerPhone), settings.OtpMaxSendsPerPhone, otpSends, v => settings.OtpMaxSendsPerPhone = v);
        Set(log, nameof(settings.OtpMaxWrongCodesPerPhone), settings.OtpMaxWrongCodesPerPhone, otpWrong, v => settings.OtpMaxWrongCodesPerPhone = v);
        Set(log, nameof(settings.OtpLockMinutes), settings.OtpLockMinutes, otpLock, v => settings.OtpLockMinutes = v);
        Set(log, nameof(settings.TimeZoneId), settings.TimeZoneId, timeZoneId, v => settings.TimeZoneId = v);
        Set(log, nameof(settings.NearestCityMaxKm), settings.NearestCityMaxKm, nearestKm, v => settings.NearestCityMaxKm = v);
        return log;
    }

    /// <summary>An empty text clears the address. Otherwise one bare address, without a display name.</summary>
    private static string? Email(string value)
    {
        var email = Text(value, ErrorAlertEmailMax, "Admin alert email");
        if (email is null)
            return null;
        if (!System.Net.Mail.MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw new DomainException("Admin alert email must be a valid email address.");
        return email;
    }

    private static int NotLower(int value, int current)
    {
        if (value < current)
            throw new DomainException($"Policy version can only go up. It is {current} now.");
        return value;
    }

    private static TimeOnly WholeMinute(TimeOnly value, string label)
    {
        if (value.Second != 0 || value.Millisecond != 0)
            throw new DomainException($"{label} must be a whole minute.");
        return value;
    }

    private static int Between(int value, int max, string label, string unit)
    {
        if (value < 1 || value > max)
            throw new DomainException($"{label} must be between 1 and {max} {unit}.");
        return value;
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

    private static decimal Kilometres(decimal value)
    {
        if (value < 1 || value > MaxNearestCityMaxKm)
            throw new DomainException($"Furthest city must be between 1 and {MaxNearestCityMaxKm:0} km.");
        return TwoDecimals(value, "Furthest city");
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
/// Payout periods in market time. Weekly runs Monday to Sunday. Biweekly runs in 14-day blocks
/// counted from <see cref="BiweeklyAnchor"/>. Only payouts created before the current period starts are due.
/// </summary>
public static class PayoutCycleRules
{
    public static readonly DateOnly BiweeklyAnchor = new(2026, 1, 5);

    public static DateTimeOffset CurrentPeriodStart(PayoutCycle cycle, DateTimeOffset now)
    {
        var today = MarketClock.Today(now);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        if (cycle == PayoutCycle.Biweekly)
        {
            var weeks = (monday.DayNumber - BiweeklyAnchor.DayNumber) / 7;
            if (((weeks % 2) + 2) % 2 == 1)
                monday = monday.AddDays(-7);
        }
        return MarketClock.SessionStart(monday, TimeOnly.MinValue);
    }

    public static DateTimeOffset NextPeriodStart(PayoutCycle cycle, DateTimeOffset now) =>
        CurrentPeriodStart(cycle, now).AddDays(cycle == PayoutCycle.Biweekly ? 14 : 7);
}
