using Ganss.Xss;

namespace Tripflow.Api.Helpers;

/// <summary>
/// Sanitizes rich text HTML (from Tiptap editor) before storing in DB.
/// Allows only tags used by Tiptap StarterKit: p, strong, em, code, h1-h3, ul, ol, li, blockquote, pre, br, a.
/// </summary>
internal static class RichTextSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var s = new HtmlSanitizer();
        s.AllowedTags.Clear();
        s.AllowedTags.Add("p");
        s.AllowedTags.Add("br");
        s.AllowedTags.Add("strong");
        s.AllowedTags.Add("em");
        s.AllowedTags.Add("code");
        s.AllowedTags.Add("h1");
        s.AllowedTags.Add("h2");
        s.AllowedTags.Add("h3");
        s.AllowedTags.Add("ul");
        s.AllowedTags.Add("ol");
        s.AllowedTags.Add("li");
        s.AllowedTags.Add("blockquote");
        s.AllowedTags.Add("pre");
        s.AllowedTags.Add("a");
        s.AllowedTags.Add("span");
        s.AllowedTags.Add("mark");
        s.AllowedAttributes.Clear();
        s.AllowedAttributes.Add("href");
        s.AllowedAttributes.Add("target");
        s.AllowedAttributes.Add("rel");
        s.AllowedAttributes.Add("style");
        s.AllowedCssProperties.Clear();
        s.AllowedCssProperties.Add("color");
        s.AllowedCssProperties.Add("background-color");
        return s;
    }

    /// <summary>
    /// Sanitizes rich text for storage. Plain text (no HTML tags) is returned as-is after trim.
    /// </summary>
    internal static string? Sanitize(string? value)
    {
        if (value is null || string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return null;

        if (!trimmed.Contains('<') || !trimmed.Contains('>'))
            return trimmed;

        var sanitized = Sanitizer.Sanitize(trimmed);
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized.Trim();
    }
}
