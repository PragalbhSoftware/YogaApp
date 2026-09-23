using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Admin;

public class DashboardModel : PageModel
{
    private readonly IAdminReportApi _reports;

    public DashboardModel(IAdminReportApi reports)
    {
        _reports = reports;
    }

    public AdminReportDto? Summary { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await _reports.SummaryAsync(cancellationToken);
        if (!result.Ok || result.Data is null || result.Data.BookingsByStatus is null || result.Data.PendingPayouts is null)
        {
            Error = result.Error ?? UiCopy.GenericError;
            return;
        }

        Summary = result.Data;
    }
}
