using Magpie.Accounts.Domain;
using Magpie.Mediator;

namespace Magpie.Accounts.Application.Interests;

/// <summary>Command: "from now on, these are this reader's topics".</summary>
public sealed record SetMyInterestsCommand(Guid ReaderId, IReadOnlyList<int>? CategoryIds) : ICommand<SetMyInterestsResult>;

/// <summary>A command returns little: did it work, and if not, why.</summary>
public sealed record SetMyInterestsResult(bool Saved, string? Error)
{
    public static readonly SetMyInterestsResult Done = new(true, null);

    public static SetMyInterestsResult Invalid(string error) => new(false, error);
}

public sealed class SetMyInterestsHandler(IInterestStore store, TimeProvider timeProvider)
    : IRequestHandler<SetMyInterestsCommand, SetMyInterestsResult>
{
    public async Task<SetMyInterestsResult> HandleAsync(SetMyInterestsCommand request, CancellationToken cancellationToken)
    {
        var selectable = await store.GetSelectableCategoryIdsAsync(cancellationToken);

        // The rule itself lives in the Domain. The handler only coordinates.
        if (!InterestSelection.TryCreate(request.CategoryIds, selectable, out var selection, out var error))
        {
            return SetMyInterestsResult.Invalid(error!);
        }

        await store.ReplaceAsync(request.ReaderId, selection!, timeProvider.GetUtcNow(), cancellationToken);
        return SetMyInterestsResult.Done;
    }
}
