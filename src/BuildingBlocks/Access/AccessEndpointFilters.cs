using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Nexgate.BuildingBlocks.OpenApi;

namespace Nexgate.BuildingBlocks.Access;

/// <summary>Códigos de error estables de autenticación y autorización.</summary>
public static class AccessErrorCodes
{
    public const string AuthenticationRequired = "authentication_required";
    public const string OrganizationContextRequired = "organization_context_required";
    public const string PermissionDenied = "permission_denied";
}

/// <summary>Indica si el usuario actual es operador de plataforma. Lo implementa Identity (se lee de la base).</summary>
public interface IPlatformAccess
{
    Task<bool> IsPlatformOperatorAsync(CancellationToken cancellationToken);
}

public static class AccessProblems
{
    public static IResult AuthenticationRequired() => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Se requiere autenticación",
        extensions: new Dictionary<string, object?> { ["code"] = AccessErrorCodes.AuthenticationRequired });

    public static IResult OrganizationContextRequired() => Results.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Se requiere una organización activa",
        extensions: new Dictionary<string, object?> { ["code"] = AccessErrorCodes.OrganizationContextRequired });

    public static IResult PermissionDenied() => Results.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "No tiene permiso para esta operación",
        extensions: new Dictionary<string, object?> { ["code"] = AccessErrorCodes.PermissionDenied });
}

public static class AccessEndpointExtensions
{
    /// <summary>401 <c>authentication_required</c> si la solicitud no tiene un token de acceso válido.</summary>
    public static TBuilder RequireAuthenticatedUser<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
                Context(context).UserId is null ? AccessProblems.AuthenticationRequired() : await next(context))
            .AcceptsAccessToken()
            .Problems(StatusCodes.Status401Unauthorized);

    /// <summary>
    /// Exige usuario autenticado (401), organización activa (403 <c>organization_context_required</c>) y el permiso
    /// indicado (403 <c>permission_denied</c>), con cualquier alcance o, si se pide, con alcance de organización.
    /// El filtrado por sitio dentro del alcance lo hace cada operación con <see cref="ICurrentPermissions.SitesFor"/>.
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission, bool atOrganization = false)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var request = Context(context);
            if (request.UserId is null)
            {
                return AccessProblems.AuthenticationRequired();
            }

            if (request.OrganizationId is null)
            {
                return AccessProblems.OrganizationContextRequired();
            }

            var permissions = context.HttpContext.RequestServices.GetRequiredService<ICurrentPermissions>();
            var allowed = atOrganization ? permissions.HasAtOrganization(permission) : permissions.Has(permission);
            return allowed ? await next(context) : AccessProblems.PermissionDenied();
        })
        .AcceptsAccessToken()
        .Problems(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden);

    /// <summary>Exige un operador de plataforma (401 sin usuario, 403 <c>permission_denied</c> si no lo es).</summary>
    public static TBuilder RequirePlatformOperator<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            if (Context(context).UserId is null)
            {
                return AccessProblems.AuthenticationRequired();
            }

            var platform = context.HttpContext.RequestServices.GetService<IPlatformAccess>();
            return platform is not null && await platform.IsPlatformOperatorAsync(context.HttpContext.RequestAborted)
                ? await next(context)
                : AccessProblems.PermissionDenied();
        })
        .AcceptsAccessToken()
        .Problems(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden);

    private static RequestContext Context(EndpointFilterInvocationContext context) =>
        context.HttpContext.RequestServices.GetRequiredService<RequestContext>();
}
