using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[Route("api/admin/reports")]
public class AdminReportsController : AdminControllerBase
{
    private readonly IAdminReportService _reports;

    public AdminReportsController(IAdminReportService reports)
    {
        _reports = reports;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AdminReportResponse>> Summary(CancellationToken cancellationToken) =>
        Ok(await _reports.SummaryAsync(cancellationToken));
}