using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Application.Ping;
using Magpie.Accounts.Tests.Fakes;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Magpie.Accounts.Tests.Integration;

// The week 1 tests, now running against the real PostgreSQL container.
[Collection(AccountsApiCollection.Name)]
public class PingEndpointTests(AccountsApiFactory factory)
{
    [Fact]
    public async Task Ping_returns_200_with_a_time_read_from_the_real_database()
    {
        using var client = factory.CreateClient();
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var response = await client.GetAsync("/api/accounts/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PingResponse>();
        Assert.NotNull(body);
        Assert.Equal("accounts", body.Service);
        Assert.InRange(body.DatabaseTimeUtc, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Readiness_is_healthy_when_the_database_is_up()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ping_returns_503_problem_details_when_the_database_is_down()
    {
        // A variation of the shared factory in which the database port is "down".
        using var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDatabaseTimeSource>();
                services.AddSingleton<IDatabaseTimeSource>(new FakeDatabaseTimeSource { IsDown = true });
            }))
            .CreateClient();

        var response = await client.GetAsync("/api/accounts/ping");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("A dependency is unavailable", problem.GetProperty("title").GetString());
        Assert.Equal("accounts", problem.GetProperty("service").GetString());
        Assert.Equal("postgresql", problem.GetProperty("dependency").GetString());
    }

    [Fact]
    public async Task Liveness_is_healthy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_address_returns_404_problem_details()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/accounts/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
