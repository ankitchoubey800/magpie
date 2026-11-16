using Magpie.Collector.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Magpie.Collector.Worker.Collecting;

/// <summary>Remembers when the loop last completed a round, so a health check can notice if it stops.</summary>
public sealed class LoopHeartbeat(TimeProvider timeProvider)
{
    private long _lastBeatTicks = timeProvider.GetUtcNow().UtcTicks;

    public DateTimeOffset LastBeatUtc => new(Interlocked.Read(ref _lastBeatTicks), TimeSpan.Zero);

    public void Beat() => Interlocked.Exchange(ref _lastBeatTicks, timeProvider.GetUtcNow().UtcTicks);
}

/// <summary>
/// The background service: wake up every few seconds for as long as the
/// program runs, and do one round. BackgroundService is ASP.NET Core's base
/// class for "a job that lives alongside the web server".
/// </summary>
public sealed class CollectionLoop(
    CollectionRunner runner, LoopHeartbeat heartbeat, IOptions<CollectorOptions> options, ILogger<CollectionLoop> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer ticks at a steady pace and never overlaps itself: if a
        // round takes longer than the interval, the next tick simply waits.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.TickSeconds));
        logger.LogInformation("Collector loop started; looking for due sources every {Seconds} seconds", options.Value.TickSeconds);

        do
        {
            try
            {
                await runner.RunOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // If a whole round fails (database down, say), log it and try
                // again on the next tick. A background loop that dies silently
                // is one of the hardest failures to notice.
                logger.LogError(exception, "A collection round failed; trying again on the next tick");
            }

            heartbeat.Beat();
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

/// <summary>
/// Liveness for a worker: "has the loop gone round recently?" A web service
/// proves it is alive by answering. A worker has to be asked a better question.
/// </summary>
public sealed class LoopHealthCheck(LoopHeartbeat heartbeat, IOptions<CollectorOptions> options, TimeProvider timeProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Allow a slow round: every due source may use its whole time budget.
        var allowed = TimeSpan.FromSeconds(options.Value.TickSeconds * 3 + options.Value.PerSourceTimeoutSeconds * 10);
        var silence = timeProvider.GetUtcNow() - heartbeat.LastBeatUtc;

        return Task.FromResult(silence <= allowed
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"The collection loop has not completed a round for {silence.TotalSeconds:0} seconds."));
    }
}
