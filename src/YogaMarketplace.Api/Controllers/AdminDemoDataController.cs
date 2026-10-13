using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Controllers;

[Route("api/admin/demo-data")]
public class AdminDemoDataController : AdminControllerBase
{
    private readonly DemoDataLoader _loader;
    private readonly IHostEnvironment _environment;
    private readonly RazorpayOptions _razorpay;

    public AdminDemoDataController(DemoDataLoader loader, IHostEnvironment environment, IOptions<RazorpayOptions> razorpay)
    {
        _loader = loader;
        _environment = environment;
        _razorpay = razorpay.Value;
    }

    /// <summary>Loads demo-data.json. Not available in Production. Sample bookings are only written with test payments.</summary>
    [HttpPost("load")]
    public async Task<ActionResult<DemoLoadResult>> Load(CancellationToken cancellationToken)
    {
        if (_environment.IsProduction())
            return NotFound();
        var testPayments = _razorpay.UseFakeGateway
            || _razorpay.KeyId.Trim().StartsWith(ProductionSafety.RazorpayTestKeyPrefix, StringComparison.OrdinalIgnoreCase);
        return Ok(await _loader.LoadAsync(testPayments, cancellationToken));
    }
}
