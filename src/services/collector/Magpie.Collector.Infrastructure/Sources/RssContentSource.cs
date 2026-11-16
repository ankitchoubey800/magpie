using Magpie.Collector.Application.Sources;

namespace Magpie.Collector.Infrastructure.Sources;

/// <summary>The plug-in for RSS and Atom feeds: fetch politely, then hand the text to the parser.</summary>
internal sealed class RssContentSource(HttpFetcher http) : IContentSource
{
    public async Task<FetchOutcome> FetchAsync(FetchRequest request, CancellationToken cancellationToken)
    {
        var document = await http.GetAsync(request.Address, request.ETag, request.LastModified, cancellationToken);
        if (document.NotModified)
        {
            return FetchOutcome.NotModified;
        }

        return new FetchOutcome(FeedParser.Parse(document.Body, request.MaxItems), document.ETag, document.LastModified);
    }
}
