using Magpie.Collector.Domain;

namespace Magpie.Collector.Application.Sources;

/// <summary>
/// THE plug-in contract (the "Strategy" pattern): one method that every kind
/// of source implements in its own way. The rest of the collector only ever
/// sees this interface, so adding a source type changes nothing else.
///
/// What a plug-in may do: call its source and translate the answer into RawItems.
/// What it may NOT do: touch the database, schedule itself, retry, or swallow
/// errors. Those jobs belong to the code around it.
/// </summary>
public interface IContentSource
{
    /// <exception cref="SourceUnavailableException">Could not be reached, or answered with a temporary error. Worth another try.</exception>
    /// <exception cref="SourceContentException">Answered, but with something we cannot read. Trying again will not help.</exception>
    Task<FetchOutcome> FetchAsync(FetchRequest request, CancellationToken cancellationToken);
}

/// <summary>Where to fetch from, and the "version labels" from last time (if any).</summary>
public sealed record FetchRequest(string Address, string? ETag, string? LastModified, int MaxItems);

public sealed record FetchOutcome(IReadOnlyList<RawItem> Items, string? ETag, string? LastModified)
{
    /// <summary>The source said "nothing has changed since your last visit".</summary>
    public static readonly FetchOutcome NotModified = new([], null, null);
}

/// <summary>Picks the plug-in for a source type (the "Factory" pattern).</summary>
public interface IContentSourceFactory
{
    IContentSource Create(SourceType type);
}

public sealed class SourceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class SourceContentException(string message, Exception? innerException = null)
    : Exception(message, innerException);
