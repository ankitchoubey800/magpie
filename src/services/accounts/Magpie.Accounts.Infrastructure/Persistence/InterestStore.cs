using Magpie.Accounts.Application.Interests;
using Magpie.Accounts.Domain;
using Microsoft.EntityFrameworkCore;

namespace Magpie.Accounts.Infrastructure.Persistence;

/// <summary>
/// The WRITE door. Notice what is NOT here: no "generic repository" class and
/// no "unit of work" class of our own. EF Core already is both: a DbSet is a
/// repository, and the DbContext saves all tracked changes in one transaction
/// when SaveChangesAsync is called. Wrapping them again would only hide them.
/// </summary>
internal sealed class InterestStore(AccountsDbContext database) : IInterestStore
{
    public async Task<IReadOnlySet<int>> GetSelectableCategoryIdsAsync(CancellationToken cancellationToken) =>
        (await database.Categories
            .AsNoTracking()
            .Where(category => category.ParentId != null)
            .Select(category => category.Id)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public async Task ReplaceAsync(
        Guid readerId, InterestSelection selection, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        // Tracked this time: we are about to change these rows.
        var existing = await database.ReaderInterests
            .Where(interest => interest.ReaderId == readerId)
            .ToListAsync(cancellationToken);

        // Remove what was dropped. Keep what stayed (and with it the original "added" date).
        database.ReaderInterests.RemoveRange(
            existing.Where(interest => !selection.CategoryIds.Contains(interest.CategoryId)));

        // Add what is new.
        var alreadyThere = existing.Select(interest => interest.CategoryId).ToHashSet();
        database.ReaderInterests.AddRange(selection.CategoryIds
            .Where(categoryId => !alreadyThere.Contains(categoryId))
            .Select(categoryId => new ReaderInterest(readerId, categoryId, weight: 1.0, nowUtc)));

        // One call, one transaction: all the removals and additions happen, or none do.
        await database.SaveChangesAsync(cancellationToken);
    }
}
