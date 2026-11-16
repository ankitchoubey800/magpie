using Magpie.Collector.Application.Collecting;
using Magpie.Collector.Infrastructure;
using Magpie.Mediator;
using Microsoft.Extensions.Options;

namespace Magpie.Collector.Worker.Collecting;

/// <summary>
/// One round of collecting: find the sources that are due and visit them, a
/// few at a time. The loop calls this on every tick; tests call it directly.
/// </summary>
public sealed class CollectionRunner(
    IServiceScopeFactory scopes, IOptions<CollectorOptions> options, ILogger<CollectionRunner> logger)
{
    public async Task<IReadOnlyList<CollectSourceResult>> RunOnceAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        IReadOnlyList<Guid> due;
        await using (var scope = scopes.CreateAsyncScope())
        {
            due = await scope.ServiceProvider.GetRequiredService<IMediator>()
                .SendAsync(new GetDueSourcesQuery(Max: settings.MaxParallelSources * 10), cancellationToken);
        }

        var results = new List<CollectSourceResult>();

        // Several sources at once, but never more than MaxParallelSources.
        await Parallel.ForEachAsync(
            due,
            new ParallelOptions { MaxDegreeOfParallelism = settings.MaxParallelSources, CancellationToken = cancellationToken },
            async (sourceId, token) =>
            {
                try
                {
                    // Each source gets its own scope, and so its own DbContext:
                    // a DbContext must never be shared between parallel tasks.
                    await using var scope = scopes.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                        .SendAsync(new CollectSourceCommand(sourceId), token);

                    lock (results)
                    {
                        results.Add(result);
                    }

                    if (result.Succeeded)
                    {
                        logger.LogInformation("Collected source {SourceId}: {NewItems} new items", result.SourceId, result.NewItems);
                    }
                    else
                    {
                        logger.LogWarning("Source {SourceId} failed: {Error}", result.SourceId, result.Error);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // The last safety net. Expected failures are handled inside the
                    // use case; this catches the unexpected (a bug, the database
                    // down) so that ONE source can never stop the round.
                    logger.LogError(exception, "Unexpected error while collecting source {SourceId}", sourceId);
                }
            });

        return results;
    }
}
