using Magpie.Mediator;

namespace Magpie.Accounts.Application.Interests;

/// <summary>Query: "show me the tree of groups and topics".</summary>
public sealed record GetCategoriesQuery : IQuery<IReadOnlyList<CategoryGroup>>;

public sealed class GetCategoriesHandler(IInterestQueries queries)
    : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryGroup>>
{
    public Task<IReadOnlyList<CategoryGroup>> HandleAsync(GetCategoriesQuery request, CancellationToken cancellationToken) =>
        queries.GetCategoryTreeAsync(cancellationToken);
}
