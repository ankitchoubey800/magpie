using Magpie.Collector.Application.Sources;

namespace Magpie.Collector.Tests.Unit;

public class RetryingContentSourceTests
{
    /// <summary>A plug-in that fails a set number of times and then succeeds.</summary>
    private sealed class FlakySource(int failuresBeforeSuccess, Func<Exception> failure) : IContentSource
    {
        public int Calls { get; private set; }

        public Task<FetchOutcome> FetchAsync(FetchRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Calls <= failuresBeforeSuccess ? throw failure() : Task.FromResult(FetchOutcome.NotModified);
        }
    }

    private static readonly FetchRequest AnyRequest = new("https://example.com/feed", null, null, 50);
    private static readonly RetrySettings ThreeTries = new(MaxAttempts: 3, FirstDelay: TimeSpan.FromSeconds(1));

    private readonly List<TimeSpan> _waits = [];

    // Records each wait instead of really waiting, and uses "no randomness" unless told otherwise.
    private RetryingContentSource Wrap(IContentSource inner, double random = 0) =>
        new(inner, ThreeTries, (wait, _) => { _waits.Add(wait); return Task.CompletedTask; }, () => random);

    [Fact]
    public async Task A_source_that_recovers_is_tried_again_until_it_works()
    {
        var flaky = new FlakySource(2, () => new SourceUnavailableException("down"));

        await Wrap(flaky).FetchAsync(AnyRequest, CancellationToken.None);

        Assert.Equal(3, flaky.Calls);
    }

    [Fact]
    public async Task Each_wait_is_twice_as_long_as_the_one_before()
    {
        var flaky = new FlakySource(2, () => new SourceUnavailableException("down"));

        await Wrap(flaky).FetchAsync(AnyRequest, CancellationToken.None);

        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], _waits.ToArray());
    }

    [Fact]
    public async Task Jitter_adds_up_to_half_as_much_again()
    {
        var flaky = new FlakySource(2, () => new SourceUnavailableException("down"));

        await Wrap(flaky, random: 1.0).FetchAsync(AnyRequest, CancellationToken.None);

        Assert.Equal([TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(3)], _waits.ToArray());
    }

    [Fact]
    public async Task After_the_last_attempt_the_failure_is_passed_on()
    {
        var alwaysDown = new FlakySource(int.MaxValue, () => new SourceUnavailableException("down"));

        await Assert.ThrowsAsync<SourceUnavailableException>(() => Wrap(alwaysDown).FetchAsync(AnyRequest, CancellationToken.None));

        Assert.Equal(3, alwaysDown.Calls);
        Assert.Equal(2, _waits.Count);
    }

    [Fact]
    public async Task Unreadable_content_is_not_retried_because_it_would_be_just_as_unreadable()
    {
        var garbage = new FlakySource(int.MaxValue, () => new SourceContentException("not a feed"));

        await Assert.ThrowsAsync<SourceContentException>(() => Wrap(garbage).FetchAsync(AnyRequest, CancellationToken.None));

        Assert.Equal(1, garbage.Calls);
        Assert.Empty(_waits);
    }
}
