using Magpie.Collector.Domain;

namespace Magpie.Collector.Application.Collecting;

/// <summary>The door to the sources table.</summary>
public interface ISourceStore
{
    /// <summary>Ids of sources whose next visit is now or overdue, most overdue first.</summary>
    Task<IReadOnlyList<Guid>> GetDueIdsAsync(DateTimeOffset nowUtc, int max, CancellationToken cancellationToken);

    Task<Source?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task SaveAsync(Source source, CancellationToken cancellationToken);
}

/// <summary>The door to the holding table for collected items.</summary>
public interface IItemStore
{
    /// <summary>Stores the items we have not seen before from this source. Returns how many were new.</summary>
    Task<int> AddNewAsync(Source source, IReadOnlyList<RawItem> items, DateTimeOffset nowUtc, CancellationToken cancellationToken);
}

/// <summary>Limits that apply to every visit, whatever the source.</summary>
public sealed record CollectionLimits(TimeSpan PerSourceTimeout, int MaxItemsPerFetch);
