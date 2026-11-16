using Magpie.Mediator;

namespace Magpie.Collector.Application.Collecting;

/// <summary>Query: "which sources should be visited now?"</summary>
public sealed record GetDueSourcesQuery(int Max) : IQuery<IReadOnlyList<Guid>>;

public sealed class GetDueSourcesHandler(ISourceStore sources, TimeProvider timeProvider)
    : IRequestHandler<GetDueSourcesQuery, IReadOnlyList<Guid>>
{
    public Task<IReadOnlyList<Guid>> HandleAsync(GetDueSourcesQuery request, CancellationToken cancellationToken) =>
        sources.GetDueIdsAsync(timeProvider.GetUtcNow(), request.Max, cancellationToken);
}
