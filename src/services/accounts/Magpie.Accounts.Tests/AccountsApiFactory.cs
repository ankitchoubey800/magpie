using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Magpie.Accounts.Tests;

/// <summary>
/// Starts the REAL Accounts API in memory (real Program.cs, real routing, real
/// error handling) and swaps only the database port for a fake.
/// </summary>
public sealed class AccountsApiFactory : WebApplicationFactory<Program>
{
    public FakeDatabaseTimeSource Database { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDatabaseTimeSource>();
            services.AddSingleton<IDatabaseTimeSource>(Database);
        });
    }
}
