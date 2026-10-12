using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Services;

internal static class AdminQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

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

    /// <summary>Counts and reads one page in SQL. <paramref name="ordered"/> needs a unique last sort key so pages never overlap.</summary>
    public static async Task<PagedResponse<TResult>> PageAsync<TRow, TResult>(
        IQueryable<TRow> ordered,
        AdminPageQuery? paging,
        Func<IReadOnlyList<TRow>, CancellationToken, Task<IReadOnlyList<TResult>>> map,
        CancellationToken cancellationToken)
    {
        var size = paging?.PageSize is int requested and >= 1 and <= MaxPageSize ? requested : DefaultPageSize;
        var page = paging?.Page is int number and >= 1 ? number : 1;
        var total = await ordered.CountAsync(cancellationToken);
        var rows = await ordered.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new PagedResponse<TResult>(await map(rows, cancellationToken), total, page, size);
    }

    public static Task<PagedResponse<TResult>> PageAsync<TRow, TResult>(
        IQueryable<TRow> ordered,
        AdminPageQuery? paging,
        Func<TRow, TResult> map,
        CancellationToken cancellationToken) =>
        PageAsync<TRow, TResult>(
            ordered,
            paging,
            (rows, _) => Task.FromResult<IReadOnlyList<TResult>>(rows.Select(map).ToList()),
            cancellationToken);
}
