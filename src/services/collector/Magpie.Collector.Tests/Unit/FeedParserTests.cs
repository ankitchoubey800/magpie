using System.Text;
using Magpie.Collector.Application.Sources;
using Magpie.Collector.Infrastructure.Sources;
using Magpie.Collector.Tests.Fakes;

namespace Magpie.Collector.Tests.Unit;

public class FeedParserTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void Reads_an_rss_feed_and_skips_the_entry_that_has_no_link()
    {
        var items = FeedParser.Parse(Bytes(SampleFeeds.Rss), maxItems: 50);

        Assert.Equal(2, items.Count);
        Assert.Equal("post-1", items[0].ExternalId);
        Assert.Equal("First & foremost", items[0].Title);
        Assert.Equal("https://blog.example.com/first", items[0].Url);
        Assert.Equal("Hello world . Second paragraph.", items[0].Summary);
        Assert.Equal("Asha", items[0].Author);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 8, 30, 0, TimeSpan.Zero), items[0].PublishedAtUtc);
    }

    [Fact]
    public void Reads_an_atom_feed_and_picks_the_link_meant_for_people()
    {
        var item = Assert.Single(FeedParser.Parse(Bytes(SampleFeeds.Atom), maxItems: 50));

        Assert.Equal("tag:example.com,2026:entry-1", item.ExternalId);
        Assert.Equal("https://example.com/entry-1", item.Url);
        Assert.Equal("Ben", item.Author);
        Assert.Equal("Short and plain.", item.Summary);
    }

    [Fact]
    public void Takes_no_more_items_than_asked()
    {
        Assert.Single(FeedParser.Parse(Bytes(SampleFeeds.Rss), maxItems: 1));
    }

    [Theory]
    [InlineData(SampleFeeds.NotXml)]
    [InlineData(SampleFeeds.XmlButNotAFeed)]
    [InlineData("")]
    public void Anything_that_is_not_a_feed_is_a_content_error(string document)
    {
        Assert.Throws<SourceContentException>(() => FeedParser.Parse(Bytes(document), maxItems: 50));
    }

    [Fact]
    public void A_document_using_dangerous_xml_features_is_refused()
    {
        Assert.Throws<SourceContentException>(() => FeedParser.Parse(Bytes(SampleFeeds.WithDtd), maxItems: 50));
    }
}
