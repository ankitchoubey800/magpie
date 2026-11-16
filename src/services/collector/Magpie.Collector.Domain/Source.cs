namespace Magpie.Collector.Domain;

/// <summary>
/// One place we collect from: a feed address and everything we remember about
/// visiting it. The rules about WHEN to visit again live here, so the worker
/// loop never has to do date arithmetic.
/// </summary>
public sealed class Source
{
    /// <summary>We never visit any source more often than this, however it is configured.</summary>
    public const int MinimumIntervalMinutes = 5;

    /// <summary>However often a source fails, we still look again at least this often.</summary>
    public static readonly TimeSpan LongestWait = TimeSpan.FromHours(24);

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public SourceType Type { get; private set; }

    public string Address { get; private set; }

    /// <summary>
    /// The slug of the topic this source is mostly about ("programming"), or
    /// null. It is a hint for the tagger in week 9. It is plain text on
    /// purpose: categories belong to the Accounts service, and services do not
    /// reach into each other's tables.
    /// </summary>
    public string? CategoryHint { get; private set; }

    public int FetchIntervalMinutes { get; private set; }

    public bool Enabled { get; private set; }

    public DateTimeOffset? LastFetchedAtUtc { get; private set; }

    public DateTimeOffset NextFetchAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    /// <summary>The "version labels" the source gave us last time. Sending them back lets it answer "nothing new".</summary>
    public string? ETag { get; private set; }

    public string? LastModified { get; private set; }

    // EF Core fills the object through this constructor when loading a row.
    private Source(Guid id, string name, SourceType type, string address, string? categoryHint, int fetchIntervalMinutes, bool enabled)
    {
        Id = id;
        Name = name;
        Type = type;
        Address = address;
        CategoryHint = categoryHint;
        FetchIntervalMinutes = fetchIntervalMinutes;
        Enabled = enabled;
    }

    public static Source Create(
        string name, SourceType type, string address, string? categoryHint, int fetchIntervalMinutes, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A source needs a name.", nameof(name));
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("A source address must be a full http or https address.", nameof(address));
        }

        if (fetchIntervalMinutes < MinimumIntervalMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fetchIntervalMinutes), $"Sources may not be fetched more often than every {MinimumIntervalMinutes} minutes.");
        }

        return new Source(Guid.CreateVersion7(), name.Trim(), type, address.Trim(), categoryHint?.Trim(), fetchIntervalMinutes, enabled: true)
        {
            NextFetchAtUtc = nowUtc, // a new source is due straight away
        };
    }

    public bool IsDue(DateTimeOffset nowUtc) => Enabled && NextFetchAtUtc <= nowUtc;

    /// <summary>The visit worked (including "nothing new"). Come back after the normal interval.</summary>
    public void RecordSuccess(DateTimeOffset nowUtc, string? etag, string? lastModified)
    {
        LastFetchedAtUtc = nowUtc;
        LastError = null;
        ConsecutiveFailures = 0;
        ETag = etag ?? ETag;
        LastModified = lastModified ?? LastModified;
        NextFetchAtUtc = nowUtc.AddMinutes(FetchIntervalMinutes);
    }

    /// <summary>
    /// The visit failed. Wait LONGER each time it fails in a row (the interval
    /// doubles), so a struggling site is left in peace instead of being hammered.
    /// </summary>
    public void RecordFailure(DateTimeOffset nowUtc, string error)
    {
        LastFetchedAtUtc = nowUtc;
        ConsecutiveFailures++;
        LastError = error.Length <= 500 ? error : error[..500];
        NextFetchAtUtc = nowUtc + WaitAfterFailures(ConsecutiveFailures);
    }

    private TimeSpan WaitAfterFailures(int failures)
    {
        // 1 failure: 1x the interval. 2: 2x. 3: 4x. 4: 8x ... never more than 24 hours.
        var doublings = Math.Min(failures - 1, 16);
        var wait = TimeSpan.FromMinutes(FetchIntervalMinutes * Math.Pow(2, doublings));
        return wait < LongestWait ? wait : LongestWait;
    }
}
