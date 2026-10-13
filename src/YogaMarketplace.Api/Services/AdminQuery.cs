using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Services;

internal static class AdminQuery
{
    public const int PageSize = 100;

    /// <summary>Page number from 1 and a page size capped at <see cref="PageSize"/>; missing values mean page 1 at the cap.</summary>
    public static (int Page, int PageSize) Paging(int? page, int? pageSize) =>
        (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? PageSize, 1, PageSize));

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
