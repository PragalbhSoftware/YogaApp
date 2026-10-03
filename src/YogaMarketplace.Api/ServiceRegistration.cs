using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using YogaMarketplace.Api.Jobs;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api;

public static class ServiceRegistration
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.Section));
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<IOtpSender, LoggingOtpSender>();
        services.AddScoped<OtpService>();
        services.AddScoped<JwtTokenService>();
        services.Configure<RefreshTokenOptions>(configuration.GetSection(RefreshTokenOptions.Section));
        services.AddScoped<RefreshTokenService>();
        services.AddSingleton<RefreshCookie>();
        services.Configure<JobsOptions>(configuration.GetSection(JobsOptions.Section));
        services.AddScoped<BackgroundJobQueue>();
        services.AddScoped<BackgroundJobRunner>();
        services.AddHostedService<BackgroundJobHostedService>();
        services.AddMemoryCache();
        services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();
        services.AddScoped<ProviderService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<BookingService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<IAdminProviderService, AdminProviderService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminBookingService, AdminBookingService>();
        services.AddScoped<IAdminTransactionService, AdminTransactionService>();
        services.AddScoped<IAdminMasterService, AdminMasterService>();
        services.AddScoped<IAdminReportService, AdminReportService>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();
        services.AddScoped<IBookingService>(sp => sp.GetRequiredService<BookingService>());
        services.AddScoped<IBookingHandshake>(sp => sp.GetRequiredService<BookingService>());
        services.AddScoped<IRazorpayWebhookHandler>(sp => sp.GetRequiredService<BookingService>());
        services.Configure<RazorpayOptions>(configuration.GetSection(RazorpayOptions.Section));

        var razorpay = configuration.GetSection(RazorpayOptions.Section).Get<RazorpayOptions>() ?? new RazorpayOptions();
        if (razorpay.UseFakeGateway)
            services.AddSingleton<IRazorpayClient, FakeRazorpayClient>();
        else
        {
            services.AddHttpClient<IRazorpayClient, RazorpayHttpClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.razorpay.com/");
                client.Timeout = TimeSpan.FromSeconds(20);
            });
        }

        return services;
    }
}
