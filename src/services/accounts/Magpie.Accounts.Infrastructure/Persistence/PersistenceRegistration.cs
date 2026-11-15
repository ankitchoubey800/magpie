using Magpie.Accounts.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Magpie.Accounts.Infrastructure.Persistence;

/// <summary>Everything that knows about EF Core lives in this folder.</summary>
public static class PersistenceRegistration
{
    internal static IServiceCollection AddAccountsDbContext(this IServiceCollection services)
    {
        // EF Core borrows connections from the SAME pool as the week 1 ping code.
        services.AddDbContext<AccountsDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>()));

        return services;
    }

    internal static IdentityBuilder AddAccountsStores(this IdentityBuilder identity) =>
        identity.AddEntityFrameworkStores<AccountsDbContext>();

    /// <summary>
    /// Brings the database schema up to date. Called ONLY by the separate
    /// "--migrate" run (see Program.cs), never during normal start-up.
    /// </summary>
    public static async Task MigrateAccountsDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }
}
