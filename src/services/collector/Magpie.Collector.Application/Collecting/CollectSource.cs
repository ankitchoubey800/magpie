using Magpie.Collector.Application.Sources;
using Magpie.Mediator;

namespace Magpie.Collector.Application.Collecting;

/// <summary>Command: "visit this one source now and store what is new".</summary>
public sealed record CollectSourceCommand(Guid SourceId) : ICommand<CollectSourceResult>;

public sealed record CollectSourceResult(Guid SourceId, bool Succeeded, int NewItems, string? Error);

/// <summary>
/// The heart of the collector. Read it as a promise: WHATEVER happens to this
/// source (slow, down, nonsense), the outcome is written on the source's own
/// row and nothing escapes to disturb the others.
/// </summary>
public sealed class CollectSourceHandler(
    ISourceStore sources,
    IItemStore items,
    IContentSourceFactory factory,
    CollectionLimits limits,
    TimeProvider timeProvider) : IRequestHandler<CollectSourceCommand, CollectSourceResult>
{
    public async Task<CollectSourceResult> HandleAsync(CollectSourceCommand request, CancellationToken cancellationToken)
    {
        var source = await sources.FindAsync(request.SourceId, cancellationToken);
        if (source is null || !source.IsDue(timeProvider.GetUtcNow()))
        {
            return new CollectSourceResult(request.SourceId, false, 0, "Source not found or not due.");
        }

        // A stopwatch for this ONE source. When it runs out, the token below is
        // cancelled and every await underneath gives up.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(limits.PerSourceTimeout);

        CollectSourceResult result;
        try
        {
            var plugIn = factory.Create(source.Type);
            var outcome = await plugIn.FetchAsync(
                new FetchRequest(source.Address, source.ETag, source.LastModified, limits.MaxItemsPerFetch), deadline.Token);

            var added = await items.AddNewAsync(source, outcome.Items, timeProvider.GetUtcNow(), deadline.Token);

            source.RecordSuccess(timeProvider.GetUtcNow(), outcome.ETag, outcome.LastModified);
            result = new CollectSourceResult(source.Id, true, added, null);
        }
        catch (Exception exception) when (exception is SourceUnavailableException or SourceContentException)
        {
            result = Fail(exception.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // OUR stopwatch ran out (not a shutdown of the whole service).
            result = Fail($"Gave up after {limits.PerSourceTimeout.TotalSeconds:0} seconds.");
        }

        // Saved with the caller's token, not the stopwatch's: even a source that
        // used up all its time must still get its failure written down.
        await sources.SaveAsync(source, cancellationToken);
        return result;

        CollectSourceResult Fail(string error)
        {
            source.RecordFailure(timeProvider.GetUtcNow(), error);
            return new CollectSourceResult(source.Id, false, 0, error);
        }
    }
}
