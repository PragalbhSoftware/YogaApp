using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitPolicies.Browse)]
[Route("api")]
public class CatalogController : ControllerBase
{
    private readonly CatalogService _catalog;
    private readonly IPlatformSettingsService _settings;

    public CatalogController(CatalogService catalog, IPlatformSettingsService settings)
    {
        _catalog = catalog;
        _settings = settings;
    }

    [HttpGet("config")]
    public async Task<ActionResult<MarketConfigResponse>> Config(CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        return Ok(new MarketConfigResponse(MarketClock.ToPublicId(settings.TimeZoneId)));
    }

    [HttpGet("areas")]
    public async Task<ActionResult<IReadOnlyList<AreaResponse>>> Areas(
        [FromQuery] Guid? cityId,
        CancellationToken cancellationToken) =>
        Ok(await _catalog.AreasAsync(cityId, cancellationToken));

    [HttpGet("catalog/states")]
    public async Task<ActionResult<IReadOnlyList<StateResponse>>> States(CancellationToken cancellationToken) =>
        Ok(await _catalog.StatesAsync(cancellationToken));

    [HttpGet("catalog/cities")]
    public async Task<ActionResult<IReadOnlyList<CityResponse>>> Cities(
        [FromQuery] Guid? stateId,
        CancellationToken cancellationToken) =>
        Ok(await _catalog.CitiesAsync(stateId, cancellationToken));

    [HttpGet("catalog/cities/nearest")]
    public async Task<ActionResult<CityResponse>> NearestCity(
        [FromQuery] double? lat,
        [FromQuery] double? lng,
        CancellationToken cancellationToken)
    {
        var city = await _catalog.NearestCityAsync(lat, lng, cancellationToken);
        return city is null ? NoContent() : Ok(city);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> Categories(CancellationToken cancellationToken) =>
        Ok(await _catalog.CategoriesAsync(cancellationToken));

    [HttpGet("policy")]
    public async Task<ActionResult<PolicyResponse>> Policy(CancellationToken cancellationToken) =>
        Ok(await _catalog.PolicyAsync(cancellationToken));

    [HttpGet("site/banner")]
    public async Task<ActionResult<BannerResponse>> Banner(CancellationToken cancellationToken) =>
        Ok(await _catalog.BannerAsync(cancellationToken));
}
