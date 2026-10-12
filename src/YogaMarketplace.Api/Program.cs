using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using YogaMarketplace.Api;
using YogaMarketplace.Api.Hosting;
using YogaMarketplace.Api.Middleware;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Infrastructure;
using YogaMarketplace.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var message = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid request." : e.ErrorMessage)
                .FirstOrDefault() ?? "Invalid request.";
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { error = message });
        };
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Yoga Marketplace API",
        Version = "v1",
        Description = "OTP auth, city and area catalog, mode-specific slots, pay-at-book, a saved customer visit address, the instructor handshake, customer cancel and reschedule, instructor availability and rate edits, and admin oversight."
    });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT from POST /api/auth/otp/verify."
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAppServices(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddTrustedProxies(builder.Configuration);
var allowedOrigins = CorsSettings.AllowedOriginsFrom(builder.Configuration, builder.Environment.IsProduction());
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("Content-Disposition")));

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents { OnTokenValidated = ActiveUserTokenValidator.ValidateAsync };
    });
builder.Services.AddAuthorization();

var productionWarnings = ProductionSafety.Check(builder.Configuration, builder.Environment.IsProduction());

var app = builder.Build();

foreach (var warning in productionWarnings)
    app.Logger.LogWarning("{ProductionWarning}", warning);

app.UseForwardedHeaders();
app.UseSecurityHeaders();
if (app.Environment.IsProduction())
    app.UseHsts();
app.UseApiErrorResponses();
app.UseMiddleware<DomainExceptionMiddleware>();
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSpaFiles();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapSpaFallback();

if (!app.Environment.IsEnvironment(DatabaseStartup.TestingEnvironment))
    await DatabaseStartup.InitializeAsync(app);
await DatabaseStartup.ScheduleRecurringJobsAsync(app);

app.Run();

public partial class Program
{
}

public static class DatabaseStartup
{
    public const string TestingEnvironment = "Testing";

    /// <summary>Tests queue the jobs they need themselves, so a recurring job never lands in their counts.</summary>
    public static bool ShouldScheduleRecurringJobs(IHostEnvironment environment) =>
        !environment.IsEnvironment(TestingEnvironment);

    public static async Task ScheduleRecurringJobsAsync(WebApplication app)
    {
        if (!ShouldScheduleRecurringJobs(app.Environment))
            return;
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<YogaMarketplace.Api.Jobs.CreditExpiryJob>()
            .EnsureScheduledAsync(DateTimeOffset.UtcNow, CancellationToken.None);
    }

    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");

        try
        {
            if (config.GetValue("Database:AutoMigrate", false))
            {
                logger.LogInformation("Applying EF Core migrations.");
                await db.Database.MigrateAsync();
            }

            await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database startup failed. Check ConnectionStrings:Default and apply EF migrations.");
            throw;
        }
    }
}
