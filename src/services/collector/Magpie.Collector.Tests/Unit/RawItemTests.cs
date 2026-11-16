using Magpie.Collector.Domain;

namespace Magpie.Collector.Tests.Unit;

public class RawItemTests
{
    [Fact]
    public void Html_is_removed_entities_are_decoded_and_spaces_are_squeezed()
    {
        var item = RawItem.TryCreate("1", "https://example.com/a", "Tom &amp; Jerry", "<p>Hello <b>world</b>.</p>\n\n  <p>Bye.</p>", null, null);

        Assert.Equal("Tom & Jerry", item!.Title);
        Assert.Equal("Hello world . Bye.", item.Summary);
    }

    [Fact]
    public void A_long_summary_is_cut_to_a_snippet()
    {
        var item = RawItem.TryCreate("1", "https://example.com/a", "Title", new string('a', 5000), null, null);

        Assert.Equal(RawItem.MaxSummaryLength, item!.Summary!.Length);
        Assert.EndsWith("…", item.Summary);
    }

    [Theory]
    [InlineData(null, "https://example.com/a")]
    [InlineData("   ", "https://example.com/a")]
    [InlineData("<b></b>", "https://example.com/a")]
    [InlineData("Title", null)]
    [InlineData("Title", "not a link")]
    [InlineData("Title", "javascript:alert(1)")]
    public void An_item_without_a_real_title_and_link_is_unusable(string? title, string? url)
    {
        Assert.Null(RawItem.TryCreate("1", url, title, null, null, null));
    }

    [Fact]
    public void Without_an_id_from_the_source_the_link_is_the_id()
    {
        var item = RawItem.TryCreate(null, "https://example.com/a", "Title", null, null, null);

        Assert.Equal("https://example.com/a", item!.ExternalId);
    }

    [Fact]
    public void Dates_are_stored_in_utc()
    {
        var india = new DateTimeOffset(2026, 11, 16, 17, 30, 0, TimeSpan.FromHours(5.5));

        var item = RawItem.TryCreate("1", "https://example.com/a", "Title", null, india, null);

        Assert.Equal(TimeSpan.Zero, item!.PublishedAtUtc!.Value.Offset);
        Assert.Equal(12, item.PublishedAtUtc.Value.Hour);
    }
}
