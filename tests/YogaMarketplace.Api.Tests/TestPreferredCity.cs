using System.Net.Http.Json;

namespace YogaMarketplace.Api.Tests;

/// <summary>Home bookings require PreferredCityId; test sign-ups call this after verify.</summary>
internal static class TestPreferredCity
{
    public static async Task PreferHarborAsync(HttpClient client)
    {
        var response = await client.PutAsJsonAsync(
            "/api/profile/preferred-city",
            new { preferredCityId = TestWorld.CityId });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
    }
}
