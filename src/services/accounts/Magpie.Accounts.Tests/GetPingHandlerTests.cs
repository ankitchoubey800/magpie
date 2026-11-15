using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Application.Ping;
using Magpie.Accounts.Tests.Fakes;

namespace Magpie.Accounts.Tests;

// Application tests: the use case runs for real, the database is a fake.
public class GetPingHandlerTests
{
    [Fact]
    public async Task Returns_the_database_time_and_the_measured_round_trip()
    {
        var clock = new ManualTimeProvider();
        var database = new FakeDatabaseTimeSource
        {
            // Pretend the query took exactly 12 milliseconds.
            OnQuery = () => clock.Advance(TimeSpan.FromMilliseconds(12)),
        };
        var handler = new GetPingHandler(database, clock);

        var response = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal("accounts", response.Service);
        Assert.Equal(database.ServerTime, response.DatabaseTimeUtc);
        Assert.Equal(12, response.RoundTripMs);
        Assert.False(response.IsSlow);
    }

    [Fact]
    public async Task Flags_a_slow_database()
    {
        var clock = new ManualTimeProvider();
        var database = new FakeDatabaseTimeSource
        {
            OnQuery = () => clock.Advance(TimeSpan.FromMilliseconds(900)),
        };
        var handler = new GetPingHandler(database, clock);

        var response = await handler.HandleAsync(CancellationToken.None);

        Assert.True(response.IsSlow);
    }

    [Fact]
    public async Task Lets_the_dependency_error_travel_upwards()
    {
        var handler = new GetPingHandler(new FakeDatabaseTimeSource { IsDown = true }, new ManualTimeProvider());

        await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => handler.HandleAsync(CancellationToken.None));
    }
}
