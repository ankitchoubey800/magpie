using Magpie.Collector.Application.Collecting;
using Magpie.Collector.Domain;
using Microsoft.EntityFrameworkCore;

namespace Magpie.Collector.Infrastructure.Persistence;

internal sealed class SourceStore(CollectorDbContext database) : ISourceStore
{
    public async Task<IReadOnlyList<Guid>> GetDueIdsAsync(DateTimeOffset nowUtc, int max, CancellationToken cancellationToken) =>
        await database.Sources
            .AsNoTracking()
            .Where(source => source.Enabled && source.NextFetchAtUtc <= nowUtc)
            .OrderBy(source => source.NextFetchAtUtc) // most overdue first, so nobody starves
            .Select(source => source.Id)
            .Take(max)
            .ToListAsync(cancellationToken);

    public Task<Source?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        database.Sources.FirstOrDefaultAsync(source => source.Id == id, cancellationToken);

    // The source was loaded through this same DbContext, so it is already tracked:
    // saving writes exactly the columns that changed.
    public Task SaveAsync(Source source, CancellationToken cancellationToken) =>
        database.SaveChangesAsync(cancellationToken);
}

internal sealed class ItemStore(CollectorDbContext database) : IItemStore
{
    public async Task<int> AddNewAsync(
        Source source, IReadOnlyList<RawItem> items, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        // A feed repeats itself: most of what we fetch, we already have. Ask the
        // database which of these ids are known, in ONE query, and keep the rest.
        var incoming = items.DistinctBy(item => item.ExternalId).ToList();
        var incomingIds = incoming.Select(item => item.ExternalId).ToList();
        var known = await database.Items
            .AsNoTracking()
            .Where(item => item.SourceId == source.Id && incomingIds.Contains(item.ExternalId))
            .Select(item => item.ExternalId)
            .ToListAsync(cancellationToken);

        var fresh = incoming
            .Where(item => !known.Contains(item.ExternalId))
            .Select(item => CollectedItem.From(source, item, nowUtc))
            .ToList();

        database.Items.AddRange(fresh);
        await database.SaveChangesAsync(cancellationToken);
        return fresh.Count;
    }
}
