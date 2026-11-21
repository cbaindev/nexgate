using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Nexgate.BuildingBlocks.Access;

/// <summary>Claims del token de acceso que emite Identity.</summary>
public static class AccessClaims
{
    public const string Subject = "sub";
    public const string Organization = "org_id";
    public const string Session = "sid";
}

/// <summary>
/// Verifica que el usuario tiene una membresía activa en la organización y devuelve sus permisos efectivos en ella,
/// o null si no hay membresía. Lo implementa Identity. Se ejecuta con el usuario y la organización ya fijados en el
/// contexto, así que la consulta pasa por RLS.
/// </summary>
public interface IRequestAccessResolver
{
    Task<IReadOnlyList<PermissionGrant>?> ResolveAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);
}

/// <summary>
/// Completa <see cref="RequestContext"/> desde el token ya validado por la autenticación: usuario (<c>sub</c>) y
/// organización activa (<c>org_id</c>), esta última solo si hay membresía activa. Sin membresía la solicitud queda sin
/// organización activa. Ningún encabezado ni parámetro del cliente puede fijar la organización.
/// </summary>
public sealed class RequestContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, RequestContext context)
    {
        var principal = http.User;
        if (principal.Identity?.IsAuthenticated == true
            && TryParse(principal.FindFirst(AccessClaims.Subject)?.Value, out var userId))
        {
            context.SetUser(userId);

            if (TryParse(principal.FindFirst(AccessClaims.Organization)?.Value, out var organizationId)
                && http.RequestServices.GetService<IRequestAccessResolver>() is { } resolver)
            {
                // La organización se fija antes de resolver para que la consulta de membresía pase por RLS.
                context.SetOrganization(organizationId);
                var grants = await resolver.ResolveAsync(userId, organizationId, http.RequestAborted);
                context.SetOrganization(grants is null ? null : organizationId, grants);
            }
        }

        await next(http);
    }

    private static bool TryParse(string? value, out Guid id) => Guid.TryParse(value, out id) && id != Guid.Empty;
}
