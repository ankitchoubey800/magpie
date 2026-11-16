using Magpie.Collector.Application.Sources;
using Magpie.Collector.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Magpie.Collector.Infrastructure.Sources;

/// <summary>
/// The Factory: the single place that knows which class handles which source
/// type. Adding a source type means one new plug-in class, one registration
/// line, and one line in this switch. Nothing else changes.
///
/// Every plug-in is handed out already wrapped in the retry decorator, so no
/// caller can forget to retry and no plug-in has to remember to.
/// </summary>
internal sealed class ContentSourceFactory(IServiceProvider services, RetrySettings retrySettings) : IContentSourceFactory
{
    public IContentSource Create(SourceType type)
    {
        IContentSource plugIn = type switch
        {
            SourceType.Rss => services.GetRequiredService<RssContentSource>(),
            SourceType.HackerNews => services.GetRequiredService<HackerNewsContentSource>(),
            _ => throw new NotSupportedException($"No plug-in is registered for source type '{type}'."),
        };

        return new RetryingContentSource(plugIn, retrySettings);
    }
}
