using System.Text.Json;
using Magpie.Collector.Application.Sources;
using Magpie.Collector.Domain;

namespace Magpie.Collector.Infrastructure.Sources;

/// <summary>
/// The plug-in for Hacker News. Its public API works in two steps: one address
/// returns a list of story ids, and each story is then fetched on its own.
/// The source's Address is the list (for example ".../v0/topstories.json").
/// </summary>
internal sealed class HackerNewsContentSource(HttpFetcher http) : IContentSource
{
    /// <summary>How many story requests may be in flight at once. Polite, and still quick.</summary>
    private const int ParallelStories = 5;

    public async Task<FetchOutcome> FetchAsync(FetchRequest request, CancellationToken cancellationToken)
    {
        var list = await http.GetAsync(request.Address, request.ETag, request.LastModified, cancellationToken);
        if (list.NotModified)
        {
            return FetchOutcome.NotModified;
        }

        long[] ids;
        try
        {
            ids = JsonSerializer.Deserialize<long[]>(list.Body) ?? [];
        }
        catch (JsonException exception)
        {
            throw new SourceContentException("The story list is not a list of numbers.", exception);
        }

        // ".../v0/topstories.json" -> ".../v0/item/123.json"
        var baseAddress = request.Address[..(request.Address.LastIndexOf('/') + 1)];
        var wanted = ids.Take(request.MaxItems).ToArray();
        var stories = new RawItem?[wanted.Length];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, wanted.Length),
            new ParallelOptions { MaxDegreeOfParallelism = ParallelStories, CancellationToken = cancellationToken },
            async (index, token) =>
            {
                try
                {
                    var story = await http.GetAsync($"{baseAddress}item/{wanted[index]}.json", null, null, token);
                    stories[index] = ToRawItem(story.Body);
                }
                catch (SourceContentException)
                {
                    // This one story is gone or unreadable. Skip it and keep the rest.
                    // (A SourceUnavailableException is NOT caught here: if the whole
                    // site is struggling, the visit should fail and be retried.)
                }
            });

        return new FetchOutcome(stories.OfType<RawItem>().ToList(), list.ETag, list.LastModified);
    }

    /// <summary>The Adapter step: one Hacker News story in, one RawItem (or nothing) out.</summary>
    private static RawItem? ToRawItem(byte[] json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var story = document.RootElement;
            if (story.ValueKind != JsonValueKind.Object || Text("type") != "story" || Flag("deleted") || Flag("dead"))
            {
                return null; // comments, job adverts, removed stories
            }

            var id = story.GetProperty("id").GetInt64();
            var discussion = $"https://news.ycombinator.com/item?id={id}";
            var published = story.TryGetProperty("time", out var time)
                ? DateTimeOffset.FromUnixTimeSeconds(time.GetInt64())
                : (DateTimeOffset?)null;

            // "Ask HN" posts have no outside link: the discussion page IS the content.
            return RawItem.TryCreate($"hn-{id}", Text("url") ?? discussion, Text("title"), Text("text"), published, Text("by"));

            string? Text(string name) =>
                story.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

            bool Flag(string name) => story.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null; // one unreadable story must not lose the other 29
        }
    }
}
