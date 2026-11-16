using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Magpie.Collector.Infrastructure.Persistence;

/// <summary>Readiness: can the collector reach its own database right now?</summary>
internal sealed class CollectorDatabaseHealthCheck(CollectorDbContext database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await database.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("PostgreSQL did not answer.");
}
