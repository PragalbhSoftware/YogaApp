using System.Text.Json;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Services;

internal readonly record struct CapturedRazorpayPayment(string PaymentId, string OrderId, long AmountPaise, string Currency);

internal static class RazorpayWebhookParser
{
    public static bool TryReadCapturedPayment(string rawBody, out CapturedRazorpayPayment payment)
    {
        payment = default;
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            throw new DomainException("Webhook payload is invalid.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("event", out var eventName))
                throw new DomainException("Webhook payload is invalid.");

            if (!string.Equals(eventName.GetString(), "payment.captured", StringComparison.Ordinal))
                return false;

            if (!root.TryGetProperty("payload", out var payload)
                || !payload.TryGetProperty("payment", out var paymentNode)
                || !paymentNode.TryGetProperty("entity", out var entity))
            {
                throw new DomainException("Webhook payload is invalid.");
            }

            var id = ReadString(entity, "id");
            var orderId = ReadString(entity, "order_id");
            var status = ReadString(entity, "status");
            var currency = ReadString(entity, "currency");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(orderId))
                throw new DomainException("Webhook payload is invalid.");
            if (!string.Equals(status, "captured", StringComparison.OrdinalIgnoreCase))
                return false;

            payment = new CapturedRazorpayPayment(id, orderId, ReadAmount(entity), string.IsNullOrWhiteSpace(currency) ? "INR" : currency);
            return true;
        }
    }

    private static string? ReadString(JsonElement entity, string name)
    {
        if (!entity.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new DomainException("Webhook payload is invalid.");
        return value.GetString();
    }

    private static long ReadAmount(JsonElement entity)
    {
        if (!entity.TryGetProperty("amount", out var amount) || amount.ValueKind != JsonValueKind.Number || !amount.TryGetInt64(out var paise))
            throw new DomainException("Webhook payload is invalid.");
        return paise;
    }
}
