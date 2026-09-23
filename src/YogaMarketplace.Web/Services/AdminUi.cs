using System.Globalization;
using YogaMarketplace.Web.Copy;

namespace YogaMarketplace.Web.Services;

public static class ProviderApprovalStatuses
{
    public const string Pending = "Pending";
    public const string Verified = "Verified";
    public const string Rejected = "Rejected";
    public const string All = "all";

    public static readonly string[] Filters = [Pending, Verified, Rejected, All];

    public static bool TryNormalize(string? status, out string canonical)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            canonical = Pending;
            return true;
        }

        var match = Filters.FirstOrDefault(item => item.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
        canonical = match ?? "";
        return match is not null;
    }

    public static string FilterLabel(string status) => status switch
    {
        Pending => UiCopy.FilterPending,
        Verified => UiCopy.FilterVerified,
        Rejected => UiCopy.FilterRejected,
        All => UiCopy.FilterAll,
        _ => status
    };
}

public static class AdminPaymentStatuses
{
    public const string Paid = "Paid";
    public const string Refunded = "Refunded";
    public const string Failed = "Failed";

    public static readonly string[] Filters = [Paid, Refunded, Failed];

    public static bool TryNormalize(string? status, out string? canonical)
    {
        canonical = null;
        if (string.IsNullOrWhiteSpace(status) || status.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
            return true;

        canonical = Filters.FirstOrDefault(item => item.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
        return canonical is not null;
    }

    public static string FilterLabel(string status) => status switch
    {
        Paid => UiCopy.FilterPaid,
        Refunded => UiCopy.FilterRefunded,
        Failed => UiCopy.FilterFailed,
        _ => status
    };
}

public static class PayoutStatuses
{
    public const string Pending = "Pending";
    public const string Exported = "Exported";
    public const string Paid = "Paid";

    public static readonly string[] Filters = [Pending, Exported, Paid];

    public static bool TryNormalize(string? status, out string canonical)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            canonical = Pending;
            return true;
        }

        var match = Filters.FirstOrDefault(item => item.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
        canonical = match ?? "";
        return match is not null;
    }

    public static string FilterLabel(string status) => status switch
    {
        Pending => UiCopy.FilterPending,
        Exported => UiCopy.FilterExported,
        Paid => UiCopy.FilterPaid,
        _ => status
    };
}

public static class AdminRoleFilters
{
    public static readonly string[] All = [AccountRoles.Customer, AccountRoles.Provider, AccountRoles.Admin];

    public static bool TryNormalize(string? role, out string? canonical)
    {
        canonical = null;
        if (string.IsNullOrWhiteSpace(role))
            return true;

        canonical = All.FirstOrDefault(item => item.Equals(role.Trim(), StringComparison.OrdinalIgnoreCase));
        return canonical is not null;
    }

    public static string Label(string role) => role switch
    {
        AccountRoles.Provider => UiCopy.ProviderSingular,
        AccountRoles.Admin => UiCopy.RoleAdmin,
        AccountRoles.Customer => UiCopy.RoleCustomer,
        _ => role
    };
}

public static class AdminNotices
{
    public const string Verified = "verified";
    public const string Rejected = "rejected";
    public const string AreaSaved = "area";
    public const string CategorySaved = "category";
    public const string PolicySaved = "policy";

    public static string? Text(string? notice) => notice switch
    {
        Verified => UiCopy.ProviderVerifiedNotice,
        Rejected => UiCopy.ProviderRejectedNotice,
        AreaSaved => UiCopy.AreaSavedNotice,
        CategorySaved => UiCopy.CategorySavedNotice,
        PolicySaved => UiCopy.PolicySavedNotice,
        _ => null
    };
}

public static class AdminProviderText
{
    public static bool CanVerify(string status) =>
        !string.Equals(status, ProviderApprovalStatuses.Verified, StringComparison.Ordinal);

    public static bool CanReject(string status) =>
        !string.Equals(status, ProviderApprovalStatuses.Rejected, StringComparison.Ordinal);

    public static IReadOnlyList<(string Mode, decimal? Rate)> Modes(AdminProviderDto provider)
    {
        var modes = new List<(string Mode, decimal? Rate)>(3);
        if (provider.OffersHome)
            modes.Add((SessionModes.Home, provider.HomeRate));
        if (provider.OffersStudio)
            modes.Add((SessionModes.Studio, provider.StudioRate));
        if (provider.OffersOnline)
            modes.Add((SessionModes.Online, provider.OnlineRate));
        return modes;
    }
}

public static class AdminLinks
{
    public static string? Http(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return null;
        return uri.ToString();
    }
}

public static class AdminFormat
{
    public static string Decimal(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string? Date(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string SessionWhen(DateOnly date, string start, string end) =>
        date.ToString("ddd d MMM yyyy", CultureInfo.GetCultureInfo("en-IN")) + " · " + start + "–" + end;

    public static string When(DateTimeOffset value)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        return TimeZoneInfo.ConvertTime(value, zone).ToString("d MMM yyyy, h:mm tt", CultureInfo.GetCultureInfo("en-IN"));
    }
}

public static class AdminInput
{
    public const int MaxRejectionReasonLength = 300;
    public const int MaxPolicyNoteLength = 400;
    public const int MinNameLength = 2;
    public const int MaxNameLength = 80;
    public const int MaxWindowHours = 168;

    public static bool TryReason(string? reason, out string? normalized, out string? error)
    {
        normalized = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (normalized is { Length: > MaxRejectionReasonLength })
        {
            error = UiCopy.RejectionReasonTooLong;
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryLabel(string? text, out string name, out string? error)
    {
        name = (text ?? "").Trim();
        if (name.Length is < MinNameLength or > MaxNameLength)
        {
            error = UiCopy.NameLength;
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryCity(string? text, out string? city, out string? error)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            city = null;
            error = null;
            return true;
        }

        if (!text.Trim().Equals(UiCopy.CityName, StringComparison.OrdinalIgnoreCase))
        {
            city = null;
            error = UiCopy.CityOnlyMumbai;
            return false;
        }

        city = UiCopy.CityName;
        error = null;
        return true;
    }

    public static bool TryDateRange(string? fromText, string? toText, out DateOnly? from, out DateOnly? to, out string? error)
    {
        from = null;
        to = null;
        if (!TryDate(fromText, out from, out error) || !TryDate(toText, out to, out error))
            return false;

        if (from is DateOnly start && to is DateOnly end && start > end)
        {
            error = UiCopy.DateOrder;
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryPercent(string? text, string label, out decimal value, out string? error)
    {
        value = 0;
        if (!decimal.TryParse((text ?? "").Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)
            || value is < 0 or > 100
            || decimal.Round(value, 2) != value)
        {
            error = string.Format(CultureInfo.InvariantCulture, UiCopy.PercentInvalid, label);
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryHours(string? text, string label, out int value, out string? error)
    {
        if (!int.TryParse((text ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value)
            || value is < 0 or > MaxWindowHours)
        {
            error = string.Format(CultureInfo.InvariantCulture, UiCopy.HoursInvalid, label);
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryNote(string? text, out string note, out string? error)
    {
        note = (text ?? "").Trim();
        if (note.Length > MaxPolicyNoteLength)
        {
            error = UiCopy.PolicyNoteTooLong;
            return false;
        }

        error = null;
        return true;
    }

    public static string? Join(string? first, string? second)
    {
        if (string.IsNullOrEmpty(first))
            return second;
        if (string.IsNullOrEmpty(second) || string.Equals(first, second, StringComparison.Ordinal))
            return first;
        return first + " " + second;
    }

    private static bool TryDate(string? text, out DateOnly? date, out string? error)
    {
        date = null;
        error = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        if (!DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            error = UiCopy.DateInvalid;
            return false;
        }

        date = parsed;
        return true;
    }
}
