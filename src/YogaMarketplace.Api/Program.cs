using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using YogaMarketplace.Api;
using YogaMarketplace.Api.Middleware;
using YogaMarketplace.Api.Options;
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
        Description = "OTP auth, Mumbai catalog, mode-specific slots, pay-at-book, the instructor handshake, and admin oversight."
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
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

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
    });
builder.Services.AddAuthorization();

var razorpay = builder.Configuration.GetSection(RazorpayOptions.Section).Get<RazorpayOptions>() ?? new RazorpayOptions();
if (builder.Environment.IsProduction() && razorpay.UseFakeGateway)
    throw new InvalidOperationException("Razorpay:UseFakeGateway must be false in Production.");

var app = builder.Build();

app.UseMiddleware<DomainExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

if (!app.Environment.IsEnvironment("Testing"))
    await DatabaseStartup.InitializeAsync(app);

app.Run();

public partial class Program
{
}

public static class DatabaseStartup
{
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
