using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;

namespace Nexgate.BuildingBlocks.Health;

public static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    /// <summary>
    /// /health/live: el proceso responde (no mira dependencias). /health/ready: dependencias disponibles.
    /// Ambos son anónimos y no devuelven datos de negocio ni detalles internos.
    /// </summary>
    public static IEndpointRouteBuilder MapNexgateHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains(ReadyTag) });
        return endpoints;
    }
}
