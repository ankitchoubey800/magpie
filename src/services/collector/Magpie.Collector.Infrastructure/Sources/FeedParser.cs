using System.Xml;
using System.Xml.Linq;
using Magpie.Collector.Application.Sources;
using Magpie.Collector.Domain;

namespace Magpie.Collector.Infrastructure.Sources;

/// <summary>
/// Turns the text of a feed into RawItems (the "Adapter" pattern: their shape
/// in, our shape out). It understands the two formats almost every feed uses:
/// RSS 2.0 (a list of "item") and Atom (a list of "entry").
///
/// It is a pure function: bytes in, items out, no network. That makes it the
/// easiest part of the collector to test, and the part most worth testing,
/// because feeds in the wild are messy.
/// </summary>
internal static class FeedParser
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public static IReadOnlyList<RawItem> Parse(byte[] document, int maxItems)
    {
        XDocument xml;
        try
        {
            // XML has features that let a document include files or expand to
            // gigabytes. A feed has no honest use for them, so they are off.
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 20_000_000,
            };
            using var stream = new MemoryStream(document);
            using var reader = XmlReader.Create(stream, settings);
            xml = XDocument.Load(reader);
        }
        catch (XmlException exception)
        {
            throw new SourceContentException("The answer is not a readable feed.", exception);
        }

        var root = xml.Root;
        var items = root?.Name.LocalName switch
        {
            "rss" => root.Descendants("item").Select(FromRssItem),
            "feed" when root.Name.Namespace == Atom => root.Elements(Atom + "entry").Select(FromAtomEntry),
            _ => throw new SourceContentException("The answer is XML, but neither an RSS nor an Atom feed."),
        };

        // Unusable entries (no title or link) are dropped one by one; the rest of the feed still counts.
        return items.OfType<RawItem>().Take(maxItems).ToList();
    }

    private static RawItem? FromRssItem(XElement item) => RawItem.TryCreate(
        externalId: (string?)item.Element("guid"),
        url: (string?)item.Element("link"),
        title: (string?)item.Element("title"),
        summary: (string?)item.Element("description"),
        publishedAt: ParseDate((string?)item.Element("pubDate")),
        author: (string?)item.Element("author") ?? (string?)item.Element(XName.Get("creator", "http://purl.org/dc/elements/1.1/")));

    private static RawItem? FromAtomEntry(XElement entry)
    {
        // An entry can carry several links; the page for humans is rel="alternate" (or has no rel at all).
        var link = entry.Elements(Atom + "link")
            .FirstOrDefault(l => ((string?)l.Attribute("rel") ?? "alternate") == "alternate")
            ?.Attribute("href")?.Value;

        return RawItem.TryCreate(
            externalId: (string?)entry.Element(Atom + "id"),
            url: link,
            title: (string?)entry.Element(Atom + "title"),
            summary: (string?)entry.Element(Atom + "summary") ?? (string?)entry.Element(Atom + "content"),
            publishedAt: ParseDate((string?)entry.Element(Atom + "published") ?? (string?)entry.Element(Atom + "updated")),
            author: (string?)entry.Element(Atom + "author")?.Element(Atom + "name"));
    }

    // Dates in feeds come in many spellings. If we cannot read one, we simply do not know the date.
    private static DateTimeOffset? ParseDate(string? text) =>
        DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
}
