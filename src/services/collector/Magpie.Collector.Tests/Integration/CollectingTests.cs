using System.Diagnostics;
using System.Net;
using Magpie.Collector.Domain;
using Magpie.Collector.Infrastructure.Persistence;
using Magpie.Collector.Tests.Fakes;

namespace Magpie.Collector.Tests.Integration;

// The real collector against a real PostgreSQL, with a pretend internet.
[Collection(CollectorCollection.Name)]
public class CollectingTests(CollectorPostgres postgres)
{
    private const string Feed = "https://blog.example.com/feed.xml";

    [Fact]
    public async Task A_due_source_is_fetched_and_its_items_are_stored()
    {
        await using var host = await CollectorTestHost.StartAsync(postgres);
        host.Web.Returns(Feed, SampleFeeds.Rss);
        var sourceId = await host.AddSourceAsync("Example Blog", Feed, intervalMinutes: 30);

        var results = await host.RunOneRoundAsync();

        var result = Assert.Single(results);
        Assert.True(result.Succeeded);
        Assert.Equal(2, result.NewItems);

        var items = await host.ItemsAsync(sourceId);
        Assert.Equal(["post-1", "post-2"], items.Select(item => item.ExternalId).ToArray());
        Assert.Equal("First & foremost", items[0].Title);
        Assert.Equal("programming", items[0].CategoryHint);

        var source = await host.SourceAsync(sourceId);
        Assert.Null(source.LastError);
        Assert.Equal(CollectorTestHost.Start.AddMinutes(30), source.NextFetchAtUtc);
    }

    [Fact]
    public async Task A_source_that_is_not_due_yet_is_left_alone()
    {
        await using var host = await CollectorTestHost.StartAsync(postgres);
        host.Web.Returns(Feed, SampleFeeds.Rss);
        await host.AddSourceAsync("Example Blog", Feed, intervalMinutes: 30);
        await host.RunOneRoundAsync();

        host.Clock.Advance(TimeSpan.FromMinutes(29));
        var results = await host.RunOneRoundAsync();

        Assert.Empty(results);
        Assert.Equal(1, host.Web.RequestsTo(Feed));
    }

    [Fact]
    public async Task Fetching_the_same_feed_again_stores_nothing_twice()
    {
        await using var host = await CollectorTestHost.StartAsync(postgres);
        host.Web.Returns(Feed, SampleFeeds.Rss);
        var sourceId = await host.AddSourceAsync("Example Blog", Feed, intervalMinutes: 30);
        await host.RunOneRoundAsync();

        host.Clock.Advance(TimeSpan.FromMinutes(31));
        var second = Assert.Single(await host.RunOneRoundAsync());

        Assert.True(second.Succeeded);
        Assert.Equal(0, second.NewItems);
        Assert.Equal(2, (await host.ItemsAsync(sourceId)).Count);
    }

    [Fact]
    public async Task The_second_visit_asks_has_it_changed_and_accepts_no()
    {
        await using var host = await CollectorTestHost.StartAsync(postgres);
        host.Web.Returns(Feed, SampleFeeds.Rss, etag: "\"version-7\"");
        var sourceId = await host.AddSourceAsync("Example Blog", Feed, intervalMinutes: 30);
        await host.RunOneRoundAsync();
        Assert.Equal("\"version-7\"", (await host.SourceAsync(sourceId)).ETag);

        host.Clock.Advance(TimeSpan.FromMinutes(31));
        var second = Assert.Single(await host.RunOneRoundAsync());

        Assert.True(second.Succeeded); // "nothing new" is a success, not a failure
        Assert.Equal(0, second.NewItems);
        Assert.Contains(host.Web.Requests[1].Headers.IfNoneMatch, tag => tag.Tag == "\"version-7\"");
    }

    // This is the week's break-it experiment, written as a test.
    [Fact]
    public async Task One_hanging_source_and_one_talking_nonsense_do_not_stop_the_healthy_ones()
    {
        await using var host = await CollectorTestHost.StartAsync(postgres);
        host.Web
            .Returns("https://good.example/feed.xml", SampleFeeds.Rss)
            .Returns("https://also-good.example/atom.xml", SampleFeeds.Atom)
            .Returns("https://garbage.example/feed.xml", SampleFeeds.NotXml, "text/html")
            .HangsForever("https://hang.example/feed.xml")
            .ReturnsStatus("https://gone.example/feed.xml", HttpStatusCode.NotFound);
        var good = await host.AddSourceAsync("Good", "https://good.example/feed.xml");
        var alsoGood = await host.AddSourceAsync("Also good", "https://also-good.example/atom.xml");
        var garbage = await host.AddSourceAsync("Garbage", "https://garbage.example/feed.xml");
        var hang = await host.AddSourceAsync("Hang", "https://hang.example/feed.xml");
        var gone = await host.AddSourceAsync("Gone", "https://gone.example/feed.xml");

        var stopwatch = Stopwatch.StartNew();
        var results = await host.RunOneRoundAsync();
        stopwatch.Stop();

        // The healthy sources were collected in full...
        Assert.Equal(2, (await host.ItemsAsync(good)).Count);
        Assert.Single(await host.ItemsAsync(alsoGood));

        // ...each broken source has its own failure written on its own row...
        Assert.Contains("not a readable feed", (await host.SourceAsync(garbage)).LastError);
        Assert.Contains("404", (await host.SourceAsync(gone)).LastError);
        var hanging = await host.SourceAsync(hang);
        Assert.NotNull(hanging.LastError);
        Assert.Equal(1, hanging.ConsecutiveFailures);
        Assert.True(hanging.NextFetchAtUtc > host.Clock.GetUtcNow());

        // ...and the whole round was bounded by the per-source budget (2 seconds in tests), not by the hang.
        Assert.Equal(5, results.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(8), $"The round took {stopwatch.Elapsed.TotalSeconds:0.0} seconds.");
    }

    [Fact]
    public async Task A_source_that_keeps_failing_is_visited_less_and_less_often()
    {
        await using var host = await CollectorTestHost.StartAsync(postgres);
        host.Web.Returns(Feed, SampleFeeds.NotXml, "text/html");
        var sourceId = await host.AddSourceAsync("Broken", Feed, intervalMinutes: 30);

        await host.RunOneRoundAsync();                        // 1st failure: wait 30 minutes
        host.Clock.Advance(TimeSpan.FromMinutes(30));
        await host.RunOneRoundAsync();                        // 2nd failure: wait 60 minutes

        var source = await host.SourceAsync(sourceId);
        Assert.Equal(2, source.ConsecutiveFailures);
        Assert.Equal(host.Clock.GetUtcNow().AddMinutes(60), source.NextFetchAtUtc);

        // Then the site is repaired: one success wipes the slate clean.
        host.Web.Returns(Feed, SampleFeeds.Rss);
        host.Clock.Advance(TimeSpan.FromMinutes(60));
        await host.RunOneRoundAsync();
        Assert.Equal(0, (await host.SourceAsync(sourceId)).ConsecutiveFailures);
    }

    [Fact]
    public async Task A_short_outage_is_ridden_out_by_the_retry_wrapper_within_one_visit()
    {
        await using var host = await CollectorTestHost.StartAsync(postgres);
        var calls = 0;
        host.Web.On(Feed, (_, _) => Task.FromResult(++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleFeeds.Rss) }));
        var sourceId = await host.AddSourceAsync("Flaky", Feed);

        var result = Assert.Single(await host.RunOneRoundAsync());

        Assert.True(result.Succeeded);
        Assert.Equal(2, calls);
        Assert.Equal(0, (await host.SourceAsync(sourceId)).ConsecutiveFailures);
    }

    [Fact]
    public async Task Many_sources_are_collected_side_by_side_without_mixing_anything_up()
    {
        await using var host = await CollectorTestHost.StartAsync(postgres);
        var ids = new List<Guid>();
        for (var n = 1; n <= 12; n++)
        {
            var address = $"https://site{n}.example/feed.xml";
            host.Web.Returns(address, SampleFeeds.Rss.Replace("post-", $"site{n}-post-"));
            ids.Add(await host.AddSourceAsync($"Site {n}", address));
        }

        var results = await host.RunOneRoundAsync();

        Assert.Equal(12, results.Count(result => result.Succeeded));
        foreach (var (id, n) in ids.Select((id, index) => (id, index + 1)))
        {
            Assert.Equal([$"site{n}-post-1", $"site{n}-post-2"], (await host.ItemsAsync(id)).Select(item => item.ExternalId).ToArray());
        }
    }

    [Fact]
    public async Task Hacker_news_stories_are_collected_through_the_same_loop()
    {
        await using var host = await CollectorTestHost.StartAsync(postgres);
        host.Web
            .Returns("https://hn.example/v0/topstories.json", "[7]", "application/json")
            .Returns("https://hn.example/v0/item/7.json",
                """{"id":7,"type":"story","title":"A story","url":"https://example.com/story","by":"asha","time":1794830400}""", "application/json");
        var sourceId = await host.AddSourceAsync("Hacker News", "https://hn.example/v0/topstories.json", SourceType.HackerNews);

        await host.RunOneRoundAsync();

        var item = Assert.Single(await host.ItemsAsync(sourceId));
        Assert.Equal("hn-7", item.ExternalId);
        Assert.Equal("https://example.com/story", item.Url);
    }

    [Fact]
    public async Task Every_request_says_who_we_are_and_where_to_reach_us()
    {
        await using var host = await CollectorTestHost.StartAsync(postgres);
        host.Web.Returns(Feed, SampleFeeds.Rss);
        await host.AddSourceAsync("Example Blog", Feed);

        await host.RunOneRoundAsync();

        var userAgent = host.Web.Requests[0].Headers.UserAgent.ToString();
        Assert.Contains("MagpieCollector/", userAgent);
        Assert.Contains("https://magpie.test/about-our-collector", userAgent);
    }

    [Fact]
    public async Task Seeding_from_a_file_adds_new_sources_once_and_only_once()
    {
        await using var host = await CollectorTestHost.StartAsync(postgres);
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, """
            [
              // comments are allowed in the seed file
              { "name": "Example Blog", "type": "Rss", "address": "https://blog.example.com/feed.xml", "categoryHint": "programming", "fetchIntervalMinutes": 60 },
              { "name": "Hacker News", "type": "HackerNews", "address": "https://hn.example/v0/topstories.json", "categoryHint": null, "fetchIntervalMinutes": 15 }
            ]
            """);

        var first = await host.Services.SeedSourcesAsync(file);
        var second = await host.Services.SeedSourcesAsync(file);

        Assert.Equal(2, first);
        Assert.Equal(0, second);
    }
}
