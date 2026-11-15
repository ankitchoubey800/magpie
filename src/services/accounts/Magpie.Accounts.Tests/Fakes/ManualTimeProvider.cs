namespace Magpie.Accounts.Tests.Fakes;

/// <summary>
/// A clock that only moves when a test tells it to. This makes timing tests
/// exact and instant instead of slow and flaky.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private long _ticks;

    // One "timestamp unit" is one tick (100 nanoseconds), so the maths stays simple.
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public void Advance(TimeSpan amount) => _ticks += amount.Ticks;
}
