using System.Net;
using System.Text;

namespace Magpie.Collector.Tests.Fakes;

/// <summary>
/// A pretend internet. Tests say what each address answers; the collector's
/// real HttpClient talks to this instead of the network. Every request that
/// arrives is kept, so tests can check exactly what was sent.
/// </summary>
public sealed class StubWeb : HttpMessageHandler, IHttpClientFactory
{
    private readonly Dictionary<string, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _routes = [];

    public List<HttpRequestMessage> Requests { get; } = [];

    public int RequestsTo(string address) => Requests.Count(request => request.RequestUri!.AbsoluteUri == address);

    public StubWeb Returns(string address, string body, string contentType = "application/rss+xml", string? etag = null) =>
        On(address, (request, _) =>
        {
            // Behave like a real server that supports "has it changed?" questions.
            if (etag is not null && request.Headers.IfNoneMatch.Any(tag => tag.Tag == etag))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, contentType) };
            if (etag is not null)
            {
                response.Headers.TryAddWithoutValidation("ETag", etag);
            }

            return Task.FromResult(response);
        });

    public StubWeb ReturnsStatus(string address, HttpStatusCode status) =>
        On(address, (_, _) => Task.FromResult(new HttpResponseMessage(status)));

    /// <summary>A server that accepts the connection and then never answers.</summary>
    public StubWeb HangsForever(string address) =>
        On(address, async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

    public StubWeb On(string address, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer)
    {
        _routes[address] = answer;
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(request);
        }

        return _routes.TryGetValue(request.RequestUri!.AbsoluteUri, out var answer)
            ? answer(request, cancellationToken)
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    // Lets unit tests hand this object to code that asks for an IHttpClientFactory.
    public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(2) };
}
