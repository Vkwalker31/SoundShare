using Microsoft.Extensions.Diagnostics.HealthChecks;
using SoundShare.Infrastructure.Persistence;

namespace SoundShare.WebApi.Health;

internal sealed class DatabaseHealthCheck(SoundShareDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Database is unavailable.");
}
