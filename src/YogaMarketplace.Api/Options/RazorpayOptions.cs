namespace YogaMarketplace.Api.Options;

public class RazorpayOptions
{
    public const string Section = "Razorpay";

    /// <summary>Razorpay key id (public). Env: Razorpay__KeyId.</summary>
    public string KeyId { get; set; } = "";

    /// <summary>Razorpay key secret. Env: Razorpay__KeySecret. Never commit a live value.</summary>
    public string KeySecret { get; set; } = "";

    /// <summary>Webhook signing secret. Env: Razorpay__WebhookSecret. Never commit a live value.</summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>
    /// When true, orders are issued locally and no call is made to Razorpay.
    /// Signatures are still verified with KeySecret and WebhookSecret.
    /// Must stay false in Production.
    /// </summary>
    public bool UseFakeGateway { get; set; }
}
