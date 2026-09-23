using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Services;

internal static class AdminQuery
{
    public const int PageSize = 100;

    public static TEnum? ParseOptional<TEnum>(string? value, string unknownMessage) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            throw new DomainException(unknownMessage);
        return parsed;
    }

    public static TEnum ParseRequired<TEnum>(string value, string unknownMessage) where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            throw new DomainException(unknownMessage);
        return parsed;
    }
}
