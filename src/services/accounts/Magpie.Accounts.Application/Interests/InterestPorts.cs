using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Application.Interests;

// Two ports instead of one, on purpose. This is the "light" form of CQRS
// (Command Query Responsibility Segregation): the code that READS for a screen
// and the code that CHANGES data have different needs, so they get different doors.

/// <summary>
/// The READ door. Returns ready-made shapes for screens. Implementations must
/// never change data, and need none of the machinery that tracks changes.
/// </summary>
public interface IInterestQueries
{
    Task<IReadOnlyList<CategoryGroup>> GetCategoryTreeAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> GetReaderCategoryIdsAsync(Guid readerId, CancellationToken cancellationToken);
}

/// <summary>
/// The WRITE door. Works with domain objects and saves all changes of one
/// use case together, or none of them.
/// </summary>
public interface IInterestStore
{
    Task<IReadOnlySet<int>> GetSelectableCategoryIdsAsync(CancellationToken cancellationToken);

    /// <summary>Makes the reader's stored interests equal to the selection: adds the new, removes the dropped, keeps the rest.</summary>
    Task ReplaceAsync(Guid readerId, InterestSelection selection, DateTimeOffset nowUtc, CancellationToken cancellationToken);
}

// ---- Shapes made for the screen (never the domain objects themselves) ----

public sealed record CategoryGroup(int Id, string Name, IReadOnlyList<CategoryTopic> Topics);

public sealed record CategoryTopic(int Id, string Slug, string Name);

public sealed record MyInterests(IReadOnlyList<int> CategoryIds);
