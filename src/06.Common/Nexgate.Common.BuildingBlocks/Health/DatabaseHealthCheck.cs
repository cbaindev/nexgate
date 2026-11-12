using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Nexgate.Common.BuildingBlocks.Health;

/// <summary>Verifica que la base de datos acepta conexiones. No expone detalles del error.</summary>
public sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource, ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));

            await using var connection = await dataSource.OpenConnectionAsync(timeout.Token);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(timeout.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "La base de datos no está disponible");
            return HealthCheckResult.Unhealthy();
        }
    }
}
