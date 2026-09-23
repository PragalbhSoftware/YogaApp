namespace YogaMarketplace.Web.Services;

/// <summary>
/// Bookings this browser has already reviewed. The bookings list does not include a review flag.
/// </summary>
public interface IReviewedBookingStore
{
    bool Contains(Guid bookingId);
    void Remember(Guid bookingId);
    void Clear();
}

public sealed class CookieReviewedBookingStore : IReviewedBookingStore
{
    public const string CookieName = "ym.reviewed";
    private const int MaxIds = 40;

    private readonly IHttpContextAccessor _http;
    private readonly HashSet<Guid> _added = [];

    public CookieReviewedBookingStore(IHttpContextAccessor http)
    {
        _http = http;
    }

    public bool Contains(Guid bookingId) => _added.Contains(bookingId) || Parse().Contains(bookingId);

    public void Remember(Guid bookingId)
    {
        var ids = Parse();
        if (!_added.Add(bookingId) && ids.Contains(bookingId))
            return;

        ids.Remove(bookingId);
        ids.Add(bookingId);
        if (ids.Count > MaxIds)
            ids.RemoveRange(0, ids.Count - MaxIds);

        if (_http.HttpContext is not { } http)
            return;

        http.Response.Cookies.Append(CookieName, string.Join(',', ids), Options(http));
    }

    public void Clear()
    {
        _added.Clear();
        if (_http.HttpContext is not { } http)
            return;

        http.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            Path = "/",
            Secure = http.Request.IsHttps,
            SameSite = SameSiteMode.Lax
        });
    }

    private List<Guid> Parse()
    {
        var raw = _http.HttpContext?.Request.Cookies[CookieName];
        var ids = new List<Guid>();
        if (string.IsNullOrWhiteSpace(raw))
            return ids;

        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(part, out var id) && !ids.Contains(id))
                ids.Add(id);
        }

        return ids;
    }

    private static CookieOptions Options(HttpContext http) => new()
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = http.Request.IsHttps,
        Path = "/",
        MaxAge = TimeSpan.FromDays(30)
    };
}
