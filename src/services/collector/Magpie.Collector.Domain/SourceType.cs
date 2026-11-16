namespace Magpie.Collector.Domain;

/// <summary>
/// The kinds of place we know how to collect from. Each value has exactly one
/// plug-in. YouTube and Bluesky join in week 6.
/// </summary>
public enum SourceType
{
    Rss = 1,
    HackerNews = 2,
}
