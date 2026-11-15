using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Magpie.Accounts.Application.Ping;

namespace Magpie.Accounts.Tests;

// Integration tests: a real HTTP request travels through the whole Accounts API.
// A new factory per test keeps tests independent of each other.
public class PingEndpointTests
{
    [Fact]
    public async Task Ping_returns_200_with_the_database_time()
    {
        await using var factory = new AccountsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/accounts/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PingResponse>();
        Assert.NotNull(body);
        Assert.Equal("accounts", body.Service);
        Assert.Equal(factory.Database.ServerTime, body.DatabaseTimeUtc);
    }

    [Fact]
    public async Task Ping_returns_503_problem_details_when_the_database_is_down()
    {
        await using var factory = new AccountsApiFactory();
        factory.Database.IsDown = true;
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/accounts/ping");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("A dependency is unavailable", problem.GetProperty("title").GetString());
        Assert.Equal("accounts", problem.GetProperty("service").GetString());
        Assert.Equal("postgresql", problem.GetProperty("dependency").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Liveness_stays_healthy_even_when_the_database_is_down()
    {
        await using var factory = new AccountsApiFactory();
        factory.Database.IsDown = true;
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_address_returns_404_problem_details()
    {
        await using var factory = new AccountsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/accounts/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
