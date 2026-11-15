namespace Magpie.Accounts.Domain;

/// <summary>
/// One "are you there?" answer from the database: what time the database says
/// it is, and how long the question took.
///
/// This is deliberately tiny. Week 1 has no real business rules yet, so this
/// class exists to show WHERE rules live: in the Domain project, in plain C#,
/// with no database, web or framework code anywhere near it.
/// </summary>
public sealed record DatabaseHeartbeat
{
    /// <summary>A round trip slower than this is worth a warning.</summary>
    public static readonly TimeSpan SlowThreshold = TimeSpan.FromMilliseconds(250);

    public DateTimeOffset ServerTimeUtc { get; }

    public TimeSpan RoundTrip { get; }

    /// <summary>The rule: a heartbeat is "slow" when it took longer than the threshold.</summary>
    public bool IsSlow => RoundTrip > SlowThreshold;

    private DatabaseHeartbeat(DateTimeOffset serverTimeUtc, TimeSpan roundTrip)
    {
        ServerTimeUtc = serverTimeUtc;
        RoundTrip = roundTrip;
    }

    /// <summary>
    /// The only way to make a heartbeat. Because the constructor is private,
    /// an invalid heartbeat can never exist anywhere in the program.
    /// </summary>
    public static DatabaseHeartbeat Create(DateTimeOffset serverTime, TimeSpan roundTrip)
    {
        if (roundTrip < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(roundTrip), "A round trip cannot take negative time.");
        }

        return new DatabaseHeartbeat(serverTime.ToUniversalTime(), roundTrip);
    }
}
