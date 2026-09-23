using Microsoft.AspNetCore.Http;

namespace YogaMarketplace.Web.Copy;

/// <summary>
/// Home and the public instructor pages stay indexable. Every other path is noindex.
/// </summary>
public static class PageSeo
{
    public const string RobotsNoIndex = "noindex, nofollow";

    public static bool IsIndexable(PathString path) =>
        !path.HasValue
        || path == "/"
        || path == "/Index"
        || path.StartsWithSegments("/instructors");
}
