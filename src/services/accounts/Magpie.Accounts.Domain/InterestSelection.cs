namespace Magpie.Accounts.Domain;

/// <summary>
/// A reader's chosen topics, already checked against the rules. Like
/// EmailAddress, once you hold an InterestSelection you know it is valid.
/// </summary>
public sealed class InterestSelection
{
    /// <summary>Fewer than this and the feed has nothing to work with.</summary>
    public const int Minimum = 3;

    /// <summary>More than this and "interested in everything" means interested in nothing.</summary>
    public const int Maximum = 15;

    public IReadOnlySet<int> CategoryIds { get; }

    private InterestSelection(IReadOnlySet<int> categoryIds) => CategoryIds = categoryIds;

    /// <param name="chosen">What the reader sent. May contain repeats or nonsense.</param>
    /// <param name="selectable">The ids of all TOPICS that exist right now.</param>
    public static bool TryCreate(
        IEnumerable<int>? chosen,
        IReadOnlySet<int> selectable,
        out InterestSelection? selection,
        out string? error)
    {
        selection = null;
        var distinct = (chosen ?? []).ToHashSet(); // choosing the same topic twice counts once

        if (distinct.Count < Minimum)
        {
            error = $"Choose at least {Minimum} topics.";
            return false;
        }

        if (distinct.Count > Maximum)
        {
            error = $"Choose at most {Maximum} topics.";
            return false;
        }

        if (!distinct.IsSubsetOf(selectable))
        {
            error = "One or more of the chosen topics do not exist.";
            return false;
        }

        selection = new InterestSelection(distinct);
        error = null;
        return true;
    }
}
