using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Account;

public class SignInModel : PageModel
{
    private static readonly string[] Genders = ["Female", "Male", "Other"];
    private readonly IMarketplaceApi _api;
    private readonly ApiOptions _options;
    private readonly IWebHostEnvironment _environment;

    public SignInModel(IMarketplaceApi api, IOptions<ApiOptions> options, IWebHostEnvironment environment)
    {
        _api = api;
        _options = options.Value;
        _environment = environment;
    }

    [BindProperty]
    public string AccountKind { get; set; } = "existing";

    [BindProperty]
    public string? Name { get; set; }

    [BindProperty]
    public string? Gender { get; set; }

    [BindProperty]
    public string? Phone { get; set; }

    [BindProperty]
    public string? Code { get; set; }

    [BindProperty]
    public string? DevCode { get; set; }

    [BindProperty]
    public string? ReturnUrl { get; set; }

    public bool CodeSent { get; private set; }
    public string? Error { get; private set; }
    public bool Unreachable { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public bool ShowDevOtpHint => _options.ShowDevOtpHint || _environment.IsDevelopment();
    public bool IsNewUser => string.Equals(AccountKind, "new", StringComparison.OrdinalIgnoreCase);

    public string? ExpiresLocal
    {
        get
        {
            if (ExpiresAt is null)
                return null;
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            return TimeZoneInfo.ConvertTime(ExpiresAt.Value, zone).ToString("h:mm tt", CultureInfo.GetCultureInfo("en-IN"));
        }
    }

    public IActionResult OnGet(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
            return CustomerFlow.AfterSignIn(HttpContext, Role(), returnUrl, Url);
        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostRequestAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
            return CustomerFlow.AfterSignIn(HttpContext, Role(), ReturnUrl, Url);

        if (!TryValidateAccount(out var error))
        {
            Error = error;
            return Page();
        }

        var result = await _api.RequestOtpAsync(new OtpRequestDto(Phone!.Trim(), Name?.Trim(), Gender, IsNewUser), cancellationToken);
        return ShowCodeStep(result);
    }

    public async Task<IActionResult> OnPostResendAsync(CancellationToken cancellationToken)
    {
        CodeSent = true;
        if (string.IsNullOrWhiteSpace(Phone))
        {
            Error = UiCopy.PhoneRequired;
            return Page();
        }

        var result = await _api.ResendOtpAsync(Phone.Trim(), cancellationToken);
        return ShowCodeStep(result, keepStepOnError: true);
    }

    public async Task<IActionResult> OnPostVerifyAsync(CancellationToken cancellationToken)
    {
        CodeSent = true;
        if (string.IsNullOrWhiteSpace(Phone))
        {
            Error = UiCopy.PhoneRequired;
            return Page();
        }
        if (string.IsNullOrWhiteSpace(Code))
        {
            Error = UiCopy.CodeRequired;
            return Page();
        }

        var result = await _api.VerifyOtpAsync(Phone.Trim(), Code.Trim(), cancellationToken);
        if (!result.Ok || result.Data is null)
        {
            Error = result.Error ?? UiCopy.GenericError;
            Unreachable = result.Unreachable;
            return Page();
        }

        await AuthSession.SignInAsync(HttpContext, result.Data);
        return CustomerFlow.AfterSignIn(HttpContext, result.Data.User.Role, ReturnUrl, Url);
    }

    private string? Role() => User.FindFirst(ClaimTypes.Role)?.Value;

    private IActionResult ShowCodeStep(ApiResult<OtpResponseDto> result, bool keepStepOnError = false)
    {
        if (!result.Ok || result.Data is null)
        {
            Error = result.Error ?? UiCopy.GenericError;
            Unreachable = result.Unreachable;
            CodeSent = keepStepOnError;
            return Page();
        }

        CodeSent = true;
        DevCode = result.Data.DevCode;
        ExpiresAt = result.Data.ExpiresAt;
        if (string.IsNullOrWhiteSpace(Code) && !string.IsNullOrWhiteSpace(DevCode))
            Code = DevCode;
        return Page();
    }

    private bool TryValidateAccount(out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(Phone))
        {
            error = UiCopy.PhoneRequired;
            return false;
        }

        if (!IsNewUser)
            return true;

        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length < 2)
        {
            error = UiCopy.NameRequired;
            return false;
        }

        if (string.IsNullOrWhiteSpace(Gender) || !Genders.Contains(Gender, StringComparer.OrdinalIgnoreCase))
        {
            error = UiCopy.GenderRequired;
            return false;
        }

        Gender = Genders.First(g => g.Equals(Gender, StringComparison.OrdinalIgnoreCase));
        return true;
    }
}
