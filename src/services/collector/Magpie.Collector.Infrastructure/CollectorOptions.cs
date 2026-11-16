using System.ComponentModel.DataAnnotations;

namespace Magpie.Collector.Infrastructure;

/// <summary>Typed settings for the collector, from the "Collector" section, checked at start-up.</summary>
public sealed class CollectorOptions
{
    public const string SectionName = "Collector";

    /// <summary>
    /// Where the owner of a site we visit can read about this collector and reach
    /// its operator. It goes into the User-Agent header of every request. Being
    /// identifiable and contactable is the first rule of polite collecting, so
    /// the service refuses to start without it.
    /// </summary>
    [Required, Url]
    public string ContactUrl { get; set; } = "";

    /// <summary>How often the loop wakes up to look for sources that are due.</summary>
    [Range(1, 3600)]
    public int TickSeconds { get; set; } = 30;

    /// <summary>How many sources may be visited at the same time.</summary>
    [Range(1, 32)]
    public int MaxParallelSources { get; set; } = 4;

    /// <summary>The whole budget for ONE source per visit, retries included.</summary>
    [Range(1, 300)]
    public int PerSourceTimeoutSeconds { get; set; } = 20;

    /// <summary>The budget for one single web request.</summary>
    [Range(1, 120)]
    public int HttpTimeoutSeconds { get; set; } = 8;

    [Range(1, 500)]
    public int MaxItemsPerFetch { get; set; } = 50;

    /// <summary>We stop reading an answer after this many bytes. Nobody's feed needs to be bigger.</summary>
    [Range(1_000, 50_000_000)]
    public int MaxResponseBytes { get; set; } = 2_000_000;
}

public sealed class CollectorDatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required(ErrorMessage =
        "Connection string 'CollectorDb' is missing. Set it with user-secrets (local run) " +
        "or the ConnectionStrings__CollectorDb environment variable (containers).")]
    public string? CollectorDb { get; set; }
}
