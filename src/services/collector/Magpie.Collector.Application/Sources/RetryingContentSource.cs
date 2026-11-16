namespace Magpie.Collector.Application.Sources;

/// <summary>How patient the retry wrapper is.</summary>
public sealed record RetrySettings(int MaxAttempts, TimeSpan FirstDelay)
{
    public static readonly RetrySettings Default = new(MaxAttempts: 3, FirstDelay: TimeSpan.FromSeconds(1));
}

/// <summary>
/// Wraps ANY plug-in and adds "try again a few times" (the "Decorator"
/// pattern). It implements the same interface as the thing it wraps, so
/// callers cannot tell the difference, and plug-ins stay free of retry code.
///
/// Two details matter more than the loop itself:
///  - each wait is longer than the last (1s, 2s, 4s...), so a struggling site gets room to recover;
///  - each wait has a random part ("jitter"), so a thousand collectors that
///    failed together do not all come back at the same instant.
/// </summary>
public sealed class RetryingContentSource(
    IContentSource inner,
    RetrySettings settings,
    Func<TimeSpan, CancellationToken, Task>? delay = null,
    Func<double>? random = null) : IContentSource
{
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;
    private readonly Func<double> _random = random ?? Random.Shared.NextDouble;

    public async Task<FetchOutcome> FetchAsync(FetchRequest request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await inner.FetchAsync(request, cancellationToken);
            }
            catch (SourceUnavailableException) when (attempt < settings.MaxAttempts)
            {
                // Only "unavailable" is retried. Unreadable content (SourceContentException)
                // would be just as unreadable a second later, so it goes straight up.
                await _delay(DelayBefore(attempt + 1), cancellationToken);
            }
        }
    }

    /// <summary>Attempt 2 waits about 1x the first delay, attempt 3 about 2x, attempt 4 about 4x, each plus up to 50% extra.</summary>
    private TimeSpan DelayBefore(int attempt)
    {
        var growing = settings.FirstDelay * Math.Pow(2, attempt - 2);
        var jitter = growing * (_random() * 0.5);
        return growing + jitter;
    }
}
