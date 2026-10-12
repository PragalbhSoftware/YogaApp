namespace YogaMarketplace.Api.Options;

/// <summary>
/// Startup checks for Production. Unsafe settings stop the app instead of going live quietly;
/// settings that are allowed but worth noticing come back as warnings to log.
/// </summary>
public static class ProductionSafety
{
    public const string RazorpayTestKeyPrefix = "rzp_test_";

    public static IReadOnlyList<string> Check(IConfiguration configuration, bool isProduction)
    {
        if (!isProduction)
            return [];

        var problems = new List<string>();
        if (configuration.GetValue("Database:AutoMigrate", false))
            problems.Add("Database:AutoMigrate must be false in Production. Apply migrations with the migration script.");

        var otp = configuration.GetSection(OtpOptions.Section).Get<OtpOptions>() ?? new OtpOptions();
        if (otp.ExposeCode || otp.UseFixedCode)
            problems.Add("Otp:ExposeCode and Otp:UseFixedCode must be false in Production.");

        var razorpay = configuration.GetSection(RazorpayOptions.Section).Get<RazorpayOptions>() ?? new RazorpayOptions();
        if (razorpay.UseFakeGateway)
            problems.Add("Razorpay:UseFakeGateway must be false in Production.");
        if (string.IsNullOrWhiteSpace(razorpay.KeyId)
            || string.IsNullOrWhiteSpace(razorpay.KeySecret)
            || string.IsNullOrWhiteSpace(razorpay.WebhookSecret))
            problems.Add("Razorpay:KeyId, Razorpay:KeySecret and Razorpay:WebhookSecret are required in Production.");
        if (DataProtectionSettings.ProductionProblem(configuration) is string keysProblem)
            problems.Add(keysProblem);

        if (problems.Count > 0)
            throw new InvalidOperationException(string.Join(" ", problems));

        return razorpay.KeyId.Trim().StartsWith(RazorpayTestKeyPrefix, StringComparison.OrdinalIgnoreCase)
            ? ["Razorpay is using test keys in Production. Payments are not real until live keys are set."]
            : [];
    }
}
