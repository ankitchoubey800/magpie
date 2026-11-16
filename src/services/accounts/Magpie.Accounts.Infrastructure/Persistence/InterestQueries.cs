using Magpie.Accounts.Application.Interests;
using Microsoft.EntityFrameworkCore;

namespace Magpie.Accounts.Infrastructure.Persistence;

/// <summary>
/// The READ door. Every query uses AsNoTracking(): EF Core then skips the
/// bookkeeping it needs for saving changes, because nothing here is ever saved.
/// </summary>
internal sealed class InterestQueries(AccountsDbContext database) : IInterestQueries
{
    public async Task<IReadOnlyList<CategoryGroup>> GetCategoryTreeAsync(CancellationToken cancellationToken)
    {
        // Thirty rows: load them in one query and shape the tree in memory.
        var all = await database.Categories
            .AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ToListAsync(cancellationToken);

        return all
            .Where(category => !category.IsTopic)
            .Select(group => new CategoryGroup(
                group.Id,
                group.Name,
                all.Where(topic => topic.ParentId == group.Id)
                    .Select(topic => new CategoryTopic(topic.Id, topic.Slug, topic.Name))
                    .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<int>> GetReaderCategoryIdsAsync(Guid readerId, CancellationToken cancellationToken) =>
        await database.ReaderInterests
            .AsNoTracking()
            .Where(interest => interest.ReaderId == readerId)
            .OrderBy(interest => interest.CategoryId)
            .Select(interest => interest.CategoryId) // ask the database for ONE column, not whole rows
            .ToListAsync(cancellationToken);
}
