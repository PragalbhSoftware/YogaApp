using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace YogaMarketplace.Api.Tests;

/// <summary>Reads the current settings version, then patches with it, the same way the admin SPA does.</summary>
internal static class AdminSettingsClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<SettingsBody> GetAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<SettingsBody>("/api/admin/settings", Json))!;

    public static async Task<SettingsBody> PatchAsync(HttpClient admin, Func<int, object> body)
    {
        var current = await GetAsync(admin);
        var response = await admin.PatchAsJsonAsync("/api/admin/settings", body(current.Version));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<SettingsBody>(payload, Json)!;
    }

    public static Task<SettingsBody> SetCancelWindowAsync(HttpClient admin, int hours) =>
        PatchAsync(admin, version => new { version, cancelFreeWindowHours = hours });

    /// <summary>Puts back the seeded fee terms so later tests in the class start from the same values.</summary>
    public static Task<SettingsBody> ResetFeesAsync(HttpClient admin) =>
        PatchAsync(admin, version => new
        {
            version,
            commissionPercent = 15m,
            convenienceFee = 0m,
            cancelFreeWindowHours = 12,
            rescheduleFreeWindowHours = 12,
            lateCancelFeeType = "Percent",
            lateCancelFeeValue = 50m,
            payoutCycle = "Weekly"
        });
}

internal sealed record SettingsBody(
    int Version,
    string Currency,
    decimal CommissionPercent,
    decimal ConvenienceFee,
    int CancelFreeWindowHours,
    int RescheduleFreeWindowHours,
    string LateCancelFeeType,
    decimal LateCancelFeeValue,
    string PayoutCycle,
    DateTimeOffset PayoutPeriodStart,
    string PolicyNote,
    string? BannerTitle,
    string? BannerSubtitle,
    string? BannerOffer,
    DateTimeOffset? UpdatedAt);
