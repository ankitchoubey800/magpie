using System.Net;
using System.Text.RegularExpressions;

namespace Magpie.Collector.Domain;

/// <summary>
/// One piece of content in OUR shape, whatever shape the source used.
/// Every plug-in's job ends by producing these.
///
/// It also enforces the project's content rule: a title, a SHORT snippet and a
/// link back to the original. Never the full text.
/// </summary>
public sealed partial record RawItem
{
    public const int MaxTitleLength = 300;
    public const int MaxSummaryLength = 500;

    /// <summary>The source's own id for this item. With the source id, it tells us "seen this one already".</summary>
    public string ExternalId { get; }

    public string Url { get; }

    public string Title { get; }

    public string? Summary { get; }

    public DateTimeOffset? PublishedAtUtc { get; }

    public string? Author { get; }

    private RawItem(string externalId, string url, string title, string? summary, DateTimeOffset? publishedAtUtc, string? author)
    {
        ExternalId = externalId;
        Url = url;
        Title = title;
        Summary = summary;
        PublishedAtUtc = publishedAtUtc;
        Author = author;
    }

    /// <summary>
    /// Returns null when the item is unusable (no title, or no proper link).
    /// One bad item must never spoil the rest of a feed.
    /// </summary>
    public static RawItem? TryCreate(
        string? externalId, string? url, string? title, string? summary, DateTimeOffset? publishedAt, string? author)
    {
        var cleanTitle = ToPlainText(title, MaxTitleLength);
        if (cleanTitle is null
            || !Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var link)
            || (link.Scheme != Uri.UriSchemeHttp && link.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return new RawItem(
            string.IsNullOrWhiteSpace(externalId) ? link.AbsoluteUri : externalId.Trim(),
            link.AbsoluteUri,
            cleanTitle,
            ToPlainText(summary, MaxSummaryLength),
            publishedAt?.ToUniversalTime(),
            ToPlainText(author, 100));
    }

    /// <summary>Removes HTML tags, decodes things like "&amp;amp;", squeezes spaces, and cuts to a maximum length.</summary>
    private static string? ToPlainText(string? input, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var text = WebUtility.HtmlDecode(HtmlTag().Replace(input, " "));
        text = Whitespace().Replace(text, " ").Trim();
        if (text.Length == 0)
        {
            return null;
        }

        return text.Length <= maxLength ? text : text[..(maxLength - 1)].TrimEnd() + "…";
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
