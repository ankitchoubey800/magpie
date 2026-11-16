using Magpie.Accounts.Application.Interests;
using Magpie.Accounts.Tests.Fakes;

namespace Magpie.Accounts.Tests.Unit;

public class SetMyInterestsHandlerTests
{
    private readonly FakeInterestStore _store = new();
    private readonly Guid _reader = Guid.NewGuid();

    private Task<SetMyInterestsResult> SendAsync(params int[] categoryIds) =>
        new SetMyInterestsHandler(_store, TimeProvider.System)
            .HandleAsync(new SetMyInterestsCommand(_reader, categoryIds), CancellationToken.None);

    [Fact]
    public async Task A_valid_choice_is_saved_for_that_reader()
    {
        var result = await SendAsync(101, 102, 103);

        Assert.True(result.Saved);
        Assert.Equal([101, 102, 103], _store.Saved[_reader].Order().ToArray());
    }

    [Fact]
    public async Task An_invalid_choice_is_reported_and_nothing_is_saved()
    {
        var result = await SendAsync(101, 102);

        Assert.False(result.Saved);
        Assert.Contains("at least 3", result.Error);
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task A_topic_that_does_not_exist_is_refused()
    {
        var result = await SendAsync(101, 102, 999);

        Assert.False(result.Saved);
        Assert.Empty(_store.Saved);
    }
}
