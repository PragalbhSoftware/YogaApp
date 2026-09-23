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
        services.AddScoped<ProviderService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<BookingService>();
        services.Configure<RazorpayOptions>(configuration.GetSection(RazorpayOptions.Section));

        var razorpay = configuration.GetSection(RazorpayOptions.Section).Get<RazorpayOptions>() ?? new RazorpayOptions();
        if (razorpay.UseFakeGateway)
            services.AddSingleton<IRazorpayGateway, FakeRazorpayGateway>();
        else
        {
            services.AddHttpClient<IRazorpayGateway, RazorpayHttpGateway>(client =>
            {
                client.BaseAddress = new Uri("https://api.razorpay.com/");
                client.Timeout = TimeSpan.FromSeconds(20);
            });
        }

        return services;
    }
}
