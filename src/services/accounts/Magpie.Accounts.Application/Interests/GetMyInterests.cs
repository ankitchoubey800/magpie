using Magpie.Mediator;

namespace Magpie.Accounts.Application.Interests;

/// <summary>Query: "which topics has this reader chosen?"</summary>
public sealed record GetMyInterestsQuery(Guid ReaderId) : IQuery<MyInterests>;

public sealed class GetMyInterestsHandler(IInterestQueries queries)
    : IRequestHandler<GetMyInterestsQuery, MyInterests>
{
    public async Task<MyInterests> HandleAsync(GetMyInterestsQuery request, CancellationToken cancellationToken) =>
        new(await queries.GetReaderCategoryIdsAsync(request.ReaderId, cancellationToken));
}
