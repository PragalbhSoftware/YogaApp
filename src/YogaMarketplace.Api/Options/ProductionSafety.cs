namespace YogaMarketplace.Api.Options;

/// <summary>
/// Startup checks for Production and Staging. Unsafe settings stop the app instead of going live quietly;
/// settings that are allowed but worth noticing come back as warnings to log.
/// Staging gets every Production check, must use Razorpay test keys, and may use OTP demo mode.
/// </summary>
public static class ProductionSafety
{
    public const string RazorpayTestKeyPrefix = "rzp_test_";

    /// <summary>Production and Staging run with production hardening (CORS, HSTS, no Swagger, these checks).</summary>
    public static bool AppliesTo(string environmentName) =>
        IsProduction(environmentName) || IsStaging(environmentName);

    public static IReadOnlyList<string> Check(IConfiguration configuration, string environmentName)
    {
        if (!AppliesTo(environmentName))
            return [];

        var production = IsProduction(environmentName);
        var problems = new List<string>();
        if (configuration.GetValue("Database:AutoMigrate", false))
            problems.Add($"Database:AutoMigrate must be false in {environmentName}. Apply migrations with the migration script.");

        var otp = configuration.GetSection(OtpOptions.Section).Get<OtpOptions>() ?? new OtpOptions();
        if (otp.ExposeCode || otp.UseFixedCode)
            problems.Add($"Otp:ExposeCode and Otp:UseFixedCode must be false in {environmentName}.");
        var demo = OtpDemoMode.From(otp);
        if (production && demo.IsConfigured)
            problems.Add("Otp:DemoPhones and Otp:DemoCode must be empty in Production. Demo sign-in is for Staging and Development.");
        else
            problems.AddRange(demo.ProductionProblems());

        var razorpay = configuration.GetSection(RazorpayOptions.Section).Get<RazorpayOptions>() ?? new RazorpayOptions();
        if (razorpay.UseFakeGateway)
            problems.Add($"Razorpay:UseFakeGateway must be false in {environmentName}.");
        if (string.IsNullOrWhiteSpace(razorpay.KeyId)
            || string.IsNullOrWhiteSpace(razorpay.KeySecret)
            || string.IsNullOrWhiteSpace(razorpay.WebhookSecret))
            problems.Add($"Razorpay:KeyId, Razorpay:KeySecret and Razorpay:WebhookSecret are required in {environmentName}.");
        var testKeys = razorpay.KeyId.Trim().StartsWith(RazorpayTestKeyPrefix, StringComparison.OrdinalIgnoreCase);
        if (!production && !string.IsNullOrWhiteSpace(razorpay.KeyId) && !testKeys)
            problems.Add($"Razorpay:KeyId must be a test key ({RazorpayTestKeyPrefix}...) in Staging.");
        if (DataProtectionSettings.ProductionProblem(configuration) is string keysProblem)
            problems.Add(keysProblem);

        if (problems.Count > 0)
            throw new InvalidOperationException(string.Join(" ", problems));

        return production && testKeys
            ? ["Razorpay is using test keys in Production. Payments are not real until live keys are set."]
            : [];
    }

    private static bool IsProduction(string environmentName) =>
        string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase);

    private static bool IsStaging(string environmentName) =>
        string.Equals(environmentName, Environments.Staging, StringComparison.OrdinalIgnoreCase);
}
