using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Magpie.Accounts.Tests.Integration;

[Collection(AccountsApiCollection.Name)]
public class RateLimitTests(AccountsApiFactory factory)
{
    [Fact]
    public async Task The_request_after_the_limit_is_refused_with_429_and_a_wait_time()
    {
        // A copy of the API with a limit of 3 per minute. It has its own
        // counters, so the other tests are not affected.
        using var strict = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Accounts:AuthRequestsPerMinute"] = "3",
                })));
        var session = new ApiSession(strict.CreateClient(new() { BaseAddress = new Uri("https://localhost") }));
        var body = new { email = "nobody@example.com", password = "wrong guess" };

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage last = null!;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            last = await session.PostAsync("/api/accounts/sign-in", body);
            statuses.Add(last.StatusCode);
        }

        Assert.Equal(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests],
            statuses);
        Assert.True(last.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        var problem = await last.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("rate_limited", problem.GetProperty("code").GetString());
        Assert.Equal("accounts", problem.GetProperty("service").GetString());

        // Reading is not limited: who-am-I still answers normally (401, not 429).
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.Client.GetAsync("/api/accounts/me")).StatusCode);
    }
}
