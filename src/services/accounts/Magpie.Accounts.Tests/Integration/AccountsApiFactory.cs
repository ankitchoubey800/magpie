using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Infrastructure.Persistence;
using Magpie.Accounts.Tests.Fakes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Magpie.Accounts.Tests.Integration;

/// <summary>
/// Starts the REAL Accounts API in memory against a REAL PostgreSQL that lives
/// in a throwaway Docker container (Testcontainers starts it before the tests
/// and deletes it afterwards). Only the mail server is replaced by a fake.
/// </summary>
public sealed class AccountsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18").Build();

    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Settings for this test run. They are added on top of appsettings.json.
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AccountsDb"] = _postgres.GetConnectionString(),
                ["Accounts:PublicBaseUrl"] = "https://magpie.test",
                // All tests come from one "address", so the real limit of 10 a
                // minute would trip constantly. One test lowers it on purpose.
                ["Accounts:AuthRequestsPerMinute"] = "100000",
                // Re-check the session on every request, so "a reset ends all
                // sessions" can be tested without waiting five minutes.
                ["Accounts:SessionRecheckMinutes"] = "0",
            }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);

            // A stand-in for Google, so the linking rules can be tested without
            // a Google account or an internet connection.
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, FakeGoogleHandler>(FakeGoogleHandler.SchemeName, null);
        });
    }

    /// <summary>
    /// A client that behaves like a browser on an HTTPS site: it keeps cookies
    /// between calls. The https address matters, because a cookie marked
    /// Secure is never sent back over plain http.
    /// </summary>
    public HttpClient CreateBrowserLikeClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    // xUnit calls this once, before the first test that uses the factory.
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await Services.MigrateAccountsDatabaseAsync();
    }

    // "new" because WebApplicationFactory already has a DisposeAsync of its own.
    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

/// <summary>
/// Tells xUnit: every test class marked [Collection("accounts-api")] shares ONE
/// factory, so the database container is started once for the whole run.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AccountsApiCollection : ICollectionFixture<AccountsApiFactory>
{
    public const string Name = "accounts-api";
}
