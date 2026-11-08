using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nexgate.BuildingBlocks.Access;
using Nexgate.BuildingBlocks.Tenancy;

namespace Nexgate.BuildingBlocks.Telemetry;

/// <summary>
/// Agrega la organización activa y el usuario (si existen) a los logs y trazas de la solicitud.
/// Solo se agregan identificadores (el del usuario es seudónimo): nunca correos, nombres ni otros datos personales.
/// Se ubica después de la autenticación y del contexto de la solicitud.
/// </summary>
public sealed class TelemetryEnrichmentMiddleware(
    RequestDelegate next,
    ILogger<TelemetryEnrichmentMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ICurrentOrganization organization, ICurrentUser user)
    {
        using var organizationScope = organization.Id is { } organizationId ? Enrich("organization.id", "OrganizationId", organizationId) : null;
        using var userScope = user.Id is { } userId ? Enrich("user.id", "UserId", userId) : null;
        await next(context);
    }

    private IDisposable? Enrich(string tag, string scopeKey, Guid value)
    {
        Activity.Current?.SetTag(tag, value);
        return logger.BeginScope(new LogScope(scopeKey, value));
    }
}
