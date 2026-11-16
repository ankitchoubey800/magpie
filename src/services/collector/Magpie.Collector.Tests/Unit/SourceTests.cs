using Magpie.Collector.Domain;

namespace Magpie.Collector.Tests.Unit;

public class SourceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private static Source NewSource(int intervalMinutes = 30) =>
        Source.Create("Example", SourceType.Rss, "https://example.com/feed.xml", "programming", intervalMinutes, Noon);

    [Fact]
    public void A_new_source_is_due_straight_away()
    {
        Assert.True(NewSource().IsDue(Noon));
    }

    [Fact]
    public void After_a_success_it_is_due_again_one_interval_later()
    {
        var source = NewSource(intervalMinutes: 30);

        source.RecordSuccess(Noon, etag: "\"v1\"", lastModified: null);

        Assert.False(source.IsDue(Noon.AddMinutes(29)));
        Assert.True(source.IsDue(Noon.AddMinutes(30)));
        Assert.Equal("\"v1\"", source.ETag);
        Assert.Null(source.LastError);
    }

    [Theory]
    [InlineData(1, 30)]     // first failure: the normal interval
    [InlineData(2, 60)]     // then double
    [InlineData(3, 120)]
    [InlineData(4, 240)]
    [InlineData(10, 1440)]  // never longer than 24 hours
    public void Each_failure_in_a_row_doubles_the_wait_up_to_a_day(int failures, int expectedMinutes)
    {
        var source = NewSource(intervalMinutes: 30);

        for (var i = 0; i < failures; i++)
        {
            source.RecordFailure(Noon, "The source could not be reached.");
        }

        Assert.Equal(failures, source.ConsecutiveFailures);
        Assert.Equal(Noon.AddMinutes(expectedMinutes), source.NextFetchAtUtc);
    }

    [Fact]
    public void One_success_forgives_all_earlier_failures()
    {
        var source = NewSource(intervalMinutes: 30);
        source.RecordFailure(Noon, "down");
        source.RecordFailure(Noon, "down");

        source.RecordSuccess(Noon, null, null);

        Assert.Equal(0, source.ConsecutiveFailures);
        Assert.Equal(Noon.AddMinutes(30), source.NextFetchAtUtc);
    }

    [Fact]
    public void A_very_long_error_is_cut_so_it_fits_the_column()
    {
        var source = NewSource();

        source.RecordFailure(Noon, new string('x', 5000));

        Assert.Equal(500, source.LastError!.Length);
    }

    [Fact]
    public void Nobody_may_be_fetched_more_often_than_every_five_minutes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewSource(intervalMinutes: 1));
    }

    [Theory]
    [InlineData("not an address")]
    [InlineData("ftp://example.com/feed.xml")]
    [InlineData("/relative/feed.xml")]
    public void Only_full_http_addresses_are_accepted(string address)
    {
        Assert.Throws<ArgumentException>(() => Source.Create("Example", SourceType.Rss, address, null, 30, Noon));
    }
}
