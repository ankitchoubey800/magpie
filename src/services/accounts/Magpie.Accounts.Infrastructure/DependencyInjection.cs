using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Infrastructure.Postgres;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Magpie.Accounts.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The name of the connection string in configuration.</summary>
    public const string ConnectionStringName = "AccountsDb";

    public static IServiceCollection AddAccountsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ONE data source for the whole process (singleton). It owns the
        // connection pool, so creating it per request would defeat the pool.
        // The container disposes it when the application shuts down.
        services.AddSingleton(_ =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is missing. " +
                    "Set it with user-secrets (local run) or the " +
                    $"ConnectionStrings__{ConnectionStringName} environment variable (containers).");

            return NpgsqlDataSource.Create(connectionString);
        });

        // "When someone asks for the port, give them the PostgreSQL adapter."
        services.AddScoped<IDatabaseTimeSource, PostgresTimeSource>();

        // "ready" tag = included in /health/ready, NOT in /health/live.
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready"]);

        return services;
    }
}
