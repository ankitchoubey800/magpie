using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Tests.Unit;

public class InterestSelectionTests
{
    private static readonly IReadOnlySet<int> Topics = Enumerable.Range(101, 20).ToHashSet();

    [Fact]
    public void Three_real_topics_are_accepted()
    {
        var accepted = InterestSelection.TryCreate([101, 102, 103], Topics, out var selection, out var error);

        Assert.True(accepted);
        Assert.Null(error);
        Assert.Equal(3, selection!.CategoryIds.Count);
    }

    [Fact]
    public void Choosing_the_same_topic_twice_counts_once()
    {
        var accepted = InterestSelection.TryCreate([101, 101, 102, 102, 103], Topics, out var selection, out _);

        Assert.True(accepted);
        Assert.Equal(3, selection!.CategoryIds.Count);
    }

    [Fact]
    public void Repeats_cannot_be_used_to_reach_the_minimum()
    {
        var accepted = InterestSelection.TryCreate([101, 101, 101], Topics, out _, out var error);

        Assert.False(accepted);
        Assert.Contains("at least 3", error);
    }

    [Fact]
    public void Nothing_at_all_is_rejected()
    {
        Assert.False(InterestSelection.TryCreate(null, Topics, out _, out var error));
        Assert.Contains("at least 3", error);
    }

    [Fact]
    public void More_than_the_maximum_is_rejected()
    {
        var tooMany = Enumerable.Range(101, InterestSelection.Maximum + 1);

        var accepted = InterestSelection.TryCreate(tooMany, Topics, out _, out var error);

        Assert.False(accepted);
        Assert.Contains("at most 15", error);
    }

    [Fact]
    public void An_id_that_is_not_a_topic_is_rejected()
    {
        // 1 could be a GROUP id, or simply made up. Either way it is not selectable.
        var accepted = InterestSelection.TryCreate([101, 102, 1], Topics, out _, out var error);

        Assert.False(accepted);
        Assert.Contains("do not exist", error);
    }
}
