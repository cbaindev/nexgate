using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nexgate.Common.BuildingBlocks.Tenancy;

using Nexgate.Common.BuildingBlocks.Telemetry;

namespace Nexgate.Api.Rest.Middlewares;

/// <summary>
/// Agrega la organización activa (si existe) a los logs y trazas de la solicitud.
/// Solo se agregan identificadores: nunca datos personales de las personas.
/// Debe ubicarse después de la autenticación/tenancy cuando existan.
/// </summary>
public sealed class TelemetryEnrichmentMiddleware(
    RequestDelegate next,
    ILogger<TelemetryEnrichmentMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ICurrentOrganization organization)
    {
        if (organization.Id is not { } organizationId)
        {
            await next(context);
            return;
        }

        Activity.Current?.SetTag("organization.id", organizationId);

        using (logger.BeginScope(new LogScope("OrganizationId", organizationId)))
        {
            await next(context);
        }
    }
}
