using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace YogaMarketplace.Web.Services;

public static class HomeVisitLimits
{
    public const int AddressMaxLength = 300;
    public const int LandmarkMaxLength = 160;
}

public static class CheckoutNotices
{
    public const string Abandoned = "abandoned";
    public const string Failed = "failed";
}

public sealed record BeginCheckout(
    Guid ProviderId,
    Guid SlotId,
    string Mode,
    string ProviderName,
    DateOnly Date,
    string Start,
    string End,
    string? HomeAddress,
    string? Landmark,
    string? Area = null,
    string? City = null,
    string? StudioAddress = null);

public sealed record CheckoutDraft(
    Guid CheckoutId,
    string KeyId,
    string OrderId,
    long AmountPaise,
    decimal Amount,
    string Currency,
    Guid ProviderId,
    Guid SlotId,
    string ProviderName,
    string Mode,
    DateOnly Date,
    string Start,
    string End,
    string? HomeAddress,
    string? Landmark,
    string? Area = null,
    string? City = null,
    string? StudioAddress = null);

/// <summary>Display fields for the book and pay summary. Not sent to the booking API.</summary>
public sealed record CheckoutSummary(
    string ProviderName,
    string Mode,
    string? Area,
    string? City,
    DateOnly Date,
    string Start,
    string End,
    decimal Amount,
    string? Currency,
    string? StudioAddress,
    string? HomeAddress,
    string? Landmark)
{
    public static CheckoutSummary From(SlotQuote quote) => new(
        quote.ProviderName,
        quote.Mode,
        quote.Area,
        quote.City,
        quote.Date,
        quote.Start,
        quote.End,
        quote.Rate,
        null,
        quote.StudioAddress,
        null,
        null);

    public static CheckoutSummary From(CheckoutDraft draft) => new(
        draft.ProviderName,
        draft.Mode,
        draft.Area,
        draft.City,
        draft.Date,
        draft.Start,
        draft.End,
        draft.Amount,
        draft.Currency,
        draft.StudioAddress,
        draft.HomeAddress,
        draft.Landmark);
}

public static class CheckoutDraftStore
{
    public const string TempDataKey = "booking.checkout";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Save(ITempDataDictionary tempData, CheckoutDraft draft) =>
        tempData[TempDataKey] = JsonSerializer.Serialize(draft, Json);

    public static CheckoutDraft? Peek(ITempDataDictionary tempData)
    {
        if (tempData.Peek(TempDataKey) is not string json)
            return null;
        return Read(json);
    }

    public static void Clear(ITempDataDictionary tempData) => tempData.Remove(TempDataKey);

    private static CheckoutDraft? Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CheckoutDraft>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
