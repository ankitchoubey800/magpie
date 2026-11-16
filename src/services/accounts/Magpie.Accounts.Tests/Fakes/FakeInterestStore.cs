using Magpie.Accounts.Application.Interests;
using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Tests.Fakes;

/// <summary>An in-memory stand-in for the write door.</summary>
public sealed class FakeInterestStore : IInterestStore
{
    public HashSet<int> Selectable { get; } = [101, 102, 103, 104, 201, 202];

    public Dictionary<Guid, IReadOnlySet<int>> Saved { get; } = [];

    public Task<IReadOnlySet<int>> GetSelectableCategoryIdsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<int>>(Selectable);

    public Task ReplaceAsync(Guid readerId, InterestSelection selection, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        Saved[readerId] = selection.CategoryIds;
        return Task.CompletedTask;
    }
}
