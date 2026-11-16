namespace Magpie.Collector.Tests.Fakes;

/// <summary>A calendar clock that only moves when a test says so ("it is now 31 minutes later").</summary>
public sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
