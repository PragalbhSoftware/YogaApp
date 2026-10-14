using System.Text.RegularExpressions;

namespace YogaMarketplace.Domain;

public static partial class NotificationTemplateRules
{
    /// <summary>The active template for the category wins; otherwise the active default (no category); otherwise none.</summary>
    public static NotificationTemplate? Choose(IEnumerable<NotificationTemplate> candidates, Guid? categoryId)
    {
        var active = candidates.Where(t => t.IsActive).ToList();
        return (categoryId is Guid id ? active.FirstOrDefault(t => t.CategoryId == id) : null)
            ?? active.FirstOrDefault(t => t.CategoryId is null);
    }

    /// <summary>
    /// Replaces each <c>{{key}}</c>. A key with no value throws, so a broken template never sends half-filled text.
    /// <paramref name="singleLine"/> turns line breaks in values into spaces: a subject must never carry CR/LF.
    /// </summary>
    public static string Render(string template, IReadOnlyDictionary<string, string> data, bool singleLine = false) =>
        Placeholder().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (!data.TryGetValue(key, out var value))
                throw new KeyNotFoundException($"Template placeholder {{{{{key}}}}} has no value.");
            return singleLine ? string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)) : value;
        });

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}")]
    private static partial Regex Placeholder();
}
