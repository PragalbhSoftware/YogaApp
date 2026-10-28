using System.Net;

namespace YogaMarketplace.Api.Tests;

/// <summary>
/// Own database. The no-content check must not see coordinates inserted by another test class.
/// Seeded areas have no coordinates.
/// </summary>
public class NearestCityEmptyTests : IClassFixture<YogaApiFactory>
{
    private readonly YogaApiFactory _factory;

    public NearestCityEmptyTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task No_located_city_is_no_content_and_non_finite_coordinates_are_rejected()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/catalog/cities/nearest?lat=0&lng=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/catalog/cities/nearest?lat=NaN&lng=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/catalog/cities/nearest?lat=Infinity&lng=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/catalog/cities/nearest?lat=0&lng=Infinity")).StatusCode);
    }
}
