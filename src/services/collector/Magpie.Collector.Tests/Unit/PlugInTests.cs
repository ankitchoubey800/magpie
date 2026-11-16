using System.Net;
using Magpie.Collector.Application.Sources;
using Magpie.Collector.Infrastructure;
using Magpie.Collector.Infrastructure.Sources;
using Magpie.Collector.Tests.Fakes;
using Microsoft.Extensions.Options;

namespace Magpie.Collector.Tests.Unit;

// The two plug-ins, each talking to the pretend internet. No database, no real network.
public class PlugInTests
{
    private const string Feed = "https://blog.example.com/feed.xml";
    private const string HnList = "https://hn.example/v0/topstories.json";

    private readonly StubWeb _web = new();

    private HttpFetcher Fetcher(int maxBytes = 2_000_000) =>
        new(_web, Options.Create(new CollectorOptions { ContactUrl = "https://magpie.test/bot", MaxResponseBytes = maxBytes }));

    private static FetchRequest Request(string address, string? etag = null, int maxItems = 50) => new(address, etag, null, maxItems);

    // ---------------- RSS ----------------

    [Fact]
    public async Task Rss_fetches_the_feed_and_remembers_its_version_label()
    {
        _web.Returns(Feed, SampleFeeds.Rss, etag: "\"v1\"");

        var outcome = await new RssContentSource(Fetcher()).FetchAsync(Request(Feed), CancellationToken.None);

        Assert.Equal(2, outcome.Items.Count);
        Assert.Equal("\"v1\"", outcome.ETag);
    }

    [Fact]
    public async Task Rss_sends_the_version_label_back_and_accepts_nothing_new()
    {
        _web.Returns(Feed, SampleFeeds.Rss, etag: "\"v1\"");

        var outcome = await new RssContentSource(Fetcher()).FetchAsync(Request(Feed, etag: "\"v1\""), CancellationToken.None);

        Assert.Same(FetchOutcome.NotModified, outcome);
        Assert.Contains(_web.Requests[0].Headers.IfNoneMatch, tag => tag.Tag == "\"v1\"");
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task A_temporary_server_error_means_unavailable_so_it_can_be_retried(HttpStatusCode status)
    {
        _web.ReturnsStatus(Feed, status);

        await Assert.ThrowsAsync<SourceUnavailableException>(
            () => new RssContentSource(Fetcher()).FetchAsync(Request(Feed), CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task A_definite_no_is_a_content_error_and_not_worth_retrying(HttpStatusCode status)
    {
        _web.ReturnsStatus(Feed, status);

        await Assert.ThrowsAsync<SourceContentException>(
            () => new RssContentSource(Fetcher()).FetchAsync(Request(Feed), CancellationToken.None));
    }

    [Fact]
    public async Task An_answer_bigger_than_the_limit_is_refused()
    {
        _web.Returns(Feed, new string('x', 5_000));

        await Assert.ThrowsAsync<SourceContentException>(
            () => new RssContentSource(Fetcher(maxBytes: 1_000)).FetchAsync(Request(Feed), CancellationToken.None));
    }

    [Fact]
    public async Task A_server_that_never_answers_is_reported_as_unavailable()
    {
        _web.HangsForever(Feed); // the stub's HttpClient gives up after 2 seconds

        await Assert.ThrowsAsync<SourceUnavailableException>(
            () => new RssContentSource(Fetcher()).FetchAsync(Request(Feed), CancellationToken.None));
    }

    // ---------------- Hacker News ----------------

    [Fact]
    public async Task HackerNews_turns_stories_into_items_and_skips_what_is_not_a_story()
    {
        _web.Returns(HnList, "[101, 102, 103, 104]", "application/json")
            .Returns("https://hn.example/v0/item/101.json",
                """{"id":101,"type":"story","title":"Show HN: Magpie","url":"https://example.com/magpie","by":"asha","time":1794830400}""", "application/json")
            .Returns("https://hn.example/v0/item/102.json",
                """{"id":102,"type":"story","title":"Ask HN: How do you test collectors?","text":"<p>Curious.</p>","by":"ben","time":1794830500}""", "application/json")
            .Returns("https://hn.example/v0/item/103.json", """{"id":103,"type":"comment","text":"nice"}""", "application/json")
            .Returns("https://hn.example/v0/item/104.json", """{"id":104,"type":"story","title":"Removed","dead":true}""", "application/json");

        var outcome = await new HackerNewsContentSource(Fetcher()).FetchAsync(Request(HnList), CancellationToken.None);

        Assert.Equal(2, outcome.Items.Count);
        var show = outcome.Items.Single(item => item.ExternalId == "hn-101");
        Assert.Equal("https://example.com/magpie", show.Url);
        Assert.Equal("asha", show.Author);
        // A story with no outside link points at its own discussion page.
        var ask = outcome.Items.Single(item => item.ExternalId == "hn-102");
        Assert.Equal("https://news.ycombinator.com/item?id=102", ask.Url);
        Assert.Equal("Curious.", ask.Summary);
    }

    [Fact]
    public async Task HackerNews_asks_for_no_more_stories_than_the_limit()
    {
        _web.Returns(HnList, "[1, 2, 3, 4, 5, 6]", "application/json");

        await new HackerNewsContentSource(Fetcher()).FetchAsync(Request(HnList, maxItems: 2), CancellationToken.None);

        Assert.Equal(3, _web.Requests.Count); // the list, plus two stories
    }

    [Fact]
    public async Task HackerNews_reports_a_list_that_is_not_a_list()
    {
        _web.Returns(HnList, "<html>maintenance</html>", "text/html");

        await Assert.ThrowsAsync<SourceContentException>(
            () => new HackerNewsContentSource(Fetcher()).FetchAsync(Request(HnList), CancellationToken.None));
    }
}
