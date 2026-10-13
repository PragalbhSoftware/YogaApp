using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[Route("api/admin/jobs")]
public class AdminJobsController : AdminControllerBase
{
    private readonly AdminJobService _jobs;

    public AdminJobsController(AdminJobService jobs)
    {
        _jobs = jobs;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<AdminJobResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        Ok(await _jobs.ListAsync(status, page, pageSize, cancellationToken));

    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<AdminJobResponse>> Retry(Guid id, CancellationToken cancellationToken) =>
        Ok(await _jobs.RetryAsync(id, cancellationToken));
}
