using System.Net;
using System.Net.Http.Headers;
using Magpie.Collector.Application.Sources;
using Microsoft.Extensions.Options;

namespace Magpie.Collector.Infrastructure.Sources;

/// <summary>What came back from one polite web request.</summary>
internal sealed record FetchedDocument(bool NotModified, byte[] Body, string? ETag, string? LastModified);

/// <summary>
/// The ONE place the collector talks to the outside web. Both plug-ins use it,
/// so the safety rules are written once: a time limit, a size limit, and the
/// translation of every kind of trouble into our two exception types.
/// </summary>
internal sealed class HttpFetcher(IHttpClientFactory httpClientFactory, IOptions<CollectorOptions> options)
{
    public const string ClientName = "sources";

    public async Task<FetchedDocument> GetAsync(string address, string? etag, string? lastModified, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, address);

        // "Conditional GET": hand back the version labels from last time. A
        // well-behaved server answers 304 Not Modified with an empty body,
        // which costs both sides almost nothing.
        if (etag is not null && EntityTagHeaderValue.TryParse(etag, out var parsedTag))
        {
            request.Headers.IfNoneMatch.Add(parsedTag);
        }

        if (lastModified is not null && DateTimeOffset.TryParse(lastModified, out var since))
        {
            request.Headers.IfModifiedSince = since;
        }

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);

            // ResponseHeadersRead: do not download the body yet. We want to
            // look at the status and the size first.
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return new FetchedDocument(true, [], etag, lastModified);
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout)
            {
                // The server's side, and probably temporary.
                throw new SourceUnavailableException($"The source answered {(int)response.StatusCode}.");
            }

            if (!response.IsSuccessStatusCode)
            {
                // 404, 403, 410...: asking again in a second will not change it.
                throw new SourceContentException($"The source answered {(int)response.StatusCode}.");
            }

            var body = await ReadLimitedAsync(response, cancellationToken);
            return new FetchedDocument(
                false,
                body,
                response.Headers.ETag?.ToString(),
                response.Content.Headers.LastModified?.ToString("R"));
        }
        catch (HttpRequestException exception)
        {
            // Could not connect, name not found, connection dropped...
            throw new SourceUnavailableException("The source could not be reached.", exception);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's own time limit fired (not our caller giving up).
            throw new SourceUnavailableException("The source took too long to answer.");
        }
    }

    private async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var limit = options.Value.MaxResponseBytes;
        if (response.Content.Headers.ContentLength > limit)
        {
            throw new SourceContentException($"The answer is larger than {limit} bytes.");
        }

        // The declared length can be missing or a lie, so count while reading.
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                throw new SourceContentException($"The answer is larger than {limit} bytes.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
