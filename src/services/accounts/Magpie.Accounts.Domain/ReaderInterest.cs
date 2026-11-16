namespace Magpie.Accounts.Domain;

/// <summary>
/// One row: "this reader is interested in this topic". Weight is 1 for a topic
/// the reader picked by hand; the recommender (weeks 13 and 14) will later
/// raise or lower it as it learns what they actually read.
/// </summary>
public sealed class ReaderInterest
{
    public Guid ReaderId { get; private set; }

    public int CategoryId { get; private set; }

    public double Weight { get; private set; }

    public DateTimeOffset AddedAtUtc { get; private set; }

    public ReaderInterest(Guid readerId, int categoryId, double weight, DateTimeOffset addedAtUtc)
    {
        ReaderId = readerId;
        CategoryId = categoryId;
        Weight = weight;
        AddedAtUtc = addedAtUtc;
    }
}
