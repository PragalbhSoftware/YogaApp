using System.Text.Json;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Services;

internal readonly record struct CapturedRazorpayPayment(string PaymentId, string OrderId, long AmountPaise, string Currency);

/// <summary><see cref="EntityId"/> is the refund id for refund events and the payment id for payment events.</summary>
internal readonly record struct RazorpayWebhookEnvelope(string Event, string? EntityId, string? PaymentId);

internal readonly record struct FailedRazorpayPayment(string PaymentId, string OrderId);

internal readonly record struct RazorpayRefund(string RefundId, string PaymentId, long AmountPaise);

internal static class RazorpayWebhookParser
{
    public static RazorpayWebhookEnvelope ReadEnvelope(string rawBody)
    {
        using var doc = Parse(rawBody);
        var root = doc.RootElement;
        if (!root.TryGetProperty("event", out var eventName) || eventName.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(eventName.GetString()))
        {
            throw Invalid();
        }

        string? entityId = null, paymentId = null;
        if (TryGetEntity(root, "refund", out var refund))
        {
            entityId = OptionalString(refund, "id");
            paymentId = OptionalString(refund, "payment_id");
        }
        else if (TryGetEntity(root, "payment", out var payment))
        {
            entityId = OptionalString(payment, "id");
            paymentId = entityId;
        }
        return new RazorpayWebhookEnvelope(eventName.GetString()!, entityId, paymentId);
    }

    public static bool TryReadCapturedPayment(string rawBody, out CapturedRazorpayPayment payment)
    {
        payment = default;
        using var doc = Parse(rawBody);
        var root = doc.RootElement;
        if (!root.TryGetProperty("event", out var eventName))
            throw Invalid();

        if (!string.Equals(eventName.GetString(), RazorpayWire.PaymentCapturedEvent, StringComparison.Ordinal))
            return false;

        if (!TryGetEntity(root, "payment", out var entity))
            throw Invalid();

        var id = ReadString(entity, "id");
        var orderId = ReadString(entity, "order_id");
        var status = ReadString(entity, "status");
        var currency = ReadString(entity, "currency");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(orderId))
            throw Invalid();
        if (!string.Equals(status, RazorpayWire.CapturedStatus, StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(currency))
            throw Invalid();

        payment = new CapturedRazorpayPayment(id, orderId, ReadAmount(entity), currency);
        return true;
    }

    /// <summary>Null when the failed payment was not made against an order.</summary>
    public static FailedRazorpayPayment? ReadFailedPayment(string rawBody)
    {
        using var doc = Parse(rawBody);
        if (!TryGetEntity(doc.RootElement, "payment", out var entity))
            throw Invalid();
        var id = ReadString(entity, "id");
        var orderId = OptionalString(entity, "order_id");
        if (string.IsNullOrWhiteSpace(id))
            throw Invalid();
        return string.IsNullOrWhiteSpace(orderId) ? null : new FailedRazorpayPayment(id, orderId);
    }

    public static RazorpayRefund ReadRefund(string rawBody)
    {
        using var doc = Parse(rawBody);
        if (!TryGetEntity(doc.RootElement, "refund", out var entity))
            throw Invalid();
        var id = ReadString(entity, "id");
        var paymentId = ReadString(entity, "payment_id");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(paymentId))
            throw Invalid();
        return new RazorpayRefund(id, paymentId, ReadAmount(entity));
    }

    private static JsonDocument Parse(string rawBody)
    {
        try
        {
            return JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            throw Invalid();
        }
    }

    private static bool TryGetEntity(JsonElement root, string name, out JsonElement entity)
    {
        entity = default;
        return root.TryGetProperty("payload", out var payload)
            && payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(name, out var node)
            && node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty("entity", out entity)
            && entity.ValueKind == JsonValueKind.Object;
    }

    private static DomainException Invalid() => new("Webhook payload is invalid.");

    private static string? OptionalString(JsonElement entity, string name) =>
        entity.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? ReadString(JsonElement entity, string name)
    {
        if (!entity.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw Invalid();
        return value.GetString();
    }

    private static long ReadAmount(JsonElement entity)
    {
        if (!entity.TryGetProperty("amount", out var amount) || amount.ValueKind != JsonValueKind.Number || !amount.TryGetInt64(out var paise))
            throw Invalid();
        return paise;
    }
}
