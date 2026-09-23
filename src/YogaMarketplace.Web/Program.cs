using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using YogaMarketplace.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Areas");
    options.Conventions.AuthorizeFolder("/Instructors");
    options.Conventions.AuthorizeFolder("/Bookings", "Customer");
    options.Conventions.AuthorizeFolder("/Instructor", "Provider");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/sign-in";
        options.AccessDeniedPath = "/account/access-denied";
        options.Cookie.Name = "ym.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Customer", policy => policy.RequireRole(AccountRoles.Customer));
    options.AddPolicy("Provider", policy => policy.RequireRole(AccountRoles.Provider));
});
builder.Services.AddHttpContextAccessor();

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.Section));
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.Section));
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient(MarketplaceApiClient.HttpClientName, (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "http://localhost:5080" : options.BaseUrl.Trim();
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds <= 0 ? 15 : options.TimeoutSeconds);
}).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddScoped<IMarketplaceApi, MarketplaceApiClient>();
builder.Services.AddScoped<IBookingApi, BookingApiClient>();
builder.Services.AddScoped<IInstructorBookingApi, InstructorBookingApiClient>();
builder.Services.AddScoped<IReviewedBookingStore, CookieReviewedBookingStore>();
builder.Services.AddSingleton<ILocalRazorpayCheckout, LocalRazorpayCheckout>();
builder.Services.AddScoped<ISlotQuoteReader, SlotQuoteReader>();
builder.Services.AddScoped<IPaymentCheckout, PaymentCheckout>();

var app = builder.Build();

var payments = app.Services.GetRequiredService<IOptions<PaymentOptions>>().Value;
if (app.Environment.IsProduction() && payments.UseFakeCheckout)
    throw new InvalidOperationException("Payments:UseFakeCheckout must be false in Production.");

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();

public partial class Program
{
}
