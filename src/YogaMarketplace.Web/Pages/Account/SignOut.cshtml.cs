using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Account;

public class SignOutModel : PageModel
{
    private readonly IReviewedBookingStore _reviewed;

    public SignOutModel(IReviewedBookingStore reviewed)
    {
        _reviewed = reviewed;
    }

    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        _reviewed.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}
