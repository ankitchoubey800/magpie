using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Tests;

// Domain tests: no database, no web server, no fakes. They run in milliseconds.
public class DatabaseHeartbeatTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_fast_round_trip_is_not_slow()
    {
        var heartbeat = DatabaseHeartbeat.Create(Noon, TimeSpan.FromMilliseconds(5));

        Assert.False(heartbeat.IsSlow);
    }

    [Fact]
    public void A_round_trip_over_the_threshold_is_slow()
    {
        var heartbeat = DatabaseHeartbeat.Create(Noon, DatabaseHeartbeat.SlowThreshold + TimeSpan.FromMilliseconds(1));

        Assert.True(heartbeat.IsSlow);
    }

    [Fact]
    public void Server_time_is_always_stored_as_utc()
    {
        var indiaTime = new DateTimeOffset(2026, 11, 15, 17, 30, 0, TimeSpan.FromHours(5.5));

        var heartbeat = DatabaseHeartbeat.Create(indiaTime, TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, heartbeat.ServerTimeUtc.Offset);
        Assert.Equal(Noon, heartbeat.ServerTimeUtc);
    }

    [Fact]
    public void A_negative_round_trip_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DatabaseHeartbeat.Create(Noon, TimeSpan.FromMilliseconds(-1)));
    }
}
