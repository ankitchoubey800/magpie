using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Magpie.Accounts.Infrastructure.Postgres;

/// <summary>
/// Readiness check: "can I run the cheapest possible query right now?"
/// </summary>
internal sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL did not answer.", exception);
        }
    }
}
