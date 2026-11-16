namespace Magpie.Collector.Domain;

/// <summary>
/// A RawItem that has been written to the holding table, with a note of where
/// and when we got it. In week 7 this table is replaced by a Kafka topic.
/// </summary>
public sealed class CollectedItem
{
    public Guid Id { get; private set; }

    public Guid SourceId { get; private set; }

    public string ExternalId { get; private set; }

    public string Url { get; private set; }

    public string Title { get; private set; }

    public string? Summary { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public string? Author { get; private set; }

    public string? CategoryHint { get; private set; }

    public DateTimeOffset CollectedAtUtc { get; private set; }

    private CollectedItem(
        Guid id, Guid sourceId, string externalId, string url, string title, string? summary,
        DateTimeOffset? publishedAtUtc, string? author, string? categoryHint, DateTimeOffset collectedAtUtc)
    {
        Id = id;
        SourceId = sourceId;
        ExternalId = externalId;
        Url = url;
        Title = title;
        Summary = summary;
        PublishedAtUtc = publishedAtUtc;
        Author = author;
        CategoryHint = categoryHint;
        CollectedAtUtc = collectedAtUtc;
    }

    public static CollectedItem From(Source source, RawItem item, DateTimeOffset nowUtc) => new(
        Guid.CreateVersion7(), source.Id, item.ExternalId, item.Url, item.Title, item.Summary,
        item.PublishedAtUtc, item.Author, source.CategoryHint, nowUtc);
}
