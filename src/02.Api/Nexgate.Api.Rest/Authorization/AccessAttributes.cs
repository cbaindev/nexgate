using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nexgate.Common.BuildingBlocks.Security;

namespace Nexgate.Api.Rest.Authorization;

/// <summary>
/// Requisito de acceso de una acción o de un controller. Toda acción tiene exactamente uno (lo verifica una prueba de
/// arquitectura), así que una acción nueva nunca queda pública por omisión. Se evalúa como filtro de autorización:
/// antes de leer el cuerpo y de validar la entrada.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public abstract class AccessRequirementAttribute : Attribute, IAsyncAuthorizationFilter
{
    /// <summary>La operación acepta el token de acceso (el contrato OpenAPI declara el requisito de seguridad).</summary>
    public virtual bool AcceptsToken => true;

    /// <summary>El token es obligatorio (si es falso y <see cref="AcceptsToken"/> es verdadero, es opcional).</summary>
    public virtual bool RequiresToken => true;

    /// <summary>Respuestas de error que puede producir el requisito.</summary>
    public abstract IReadOnlyList<int> ProblemStatusCodes { get; }

    public abstract Task OnAuthorizationAsync(AuthorizationFilterContext context);

    protected static RequestContext Context(AuthorizationFilterContext context) =>
        context.HttpContext.RequestServices.GetRequiredService<RequestContext>();
}

/// <summary>401 <c>authentication_required</c> si la solicitud no tiene un token de acceso válido.</summary>
public sealed class RequireAuthenticatedUserAttribute : AccessRequirementAttribute
{
    public override IReadOnlyList<int> ProblemStatusCodes => [StatusCodes.Status401Unauthorized];

    public override Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (Context(context).UserId is null)
        {
            context.Result = AccessProblems.AuthenticationRequired();
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Exige usuario autenticado (401), organización activa (403 <c>organization_context_required</c>) y el permiso
/// indicado (403 <c>permission_denied</c>), con cualquier alcance o, con <see cref="AtOrganization"/>, con alcance de
/// organización. El filtrado por sitio dentro del alcance lo hace cada acción con
/// <see cref="ICurrentPermissions.SitesFor"/>.
/// </summary>
public sealed class RequirePermissionAttribute(string permission) : AccessRequirementAttribute
{
    public string Permission { get; } = permission;

    public bool AtOrganization { get; init; }

    public override IReadOnlyList<int> ProblemStatusCodes => [StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden];

    public override Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = Context(context);
        if (request.UserId is null)
        {
            context.Result = AccessProblems.AuthenticationRequired();
        }
        else if (request.OrganizationId is null)
        {
            context.Result = AccessProblems.OrganizationContextRequired();
        }
        else
        {
            var permissions = context.HttpContext.RequestServices.GetRequiredService<ICurrentPermissions>();
            var allowed = AtOrganization ? permissions.HasAtOrganization(Permission) : permissions.Has(Permission);
            if (!allowed)
            {
                context.Result = AccessProblems.PermissionDenied();
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>Exige un operador de plataforma (401 sin usuario, 403 <c>permission_denied</c> si no lo es).</summary>
public sealed class RequirePlatformOperatorAttribute : AccessRequirementAttribute
{
    public override IReadOnlyList<int> ProblemStatusCodes => [StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden];

    public override async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (Context(context).UserId is null)
        {
            context.Result = AccessProblems.AuthenticationRequired();
            return;
        }

        var platform = context.HttpContext.RequestServices.GetService<IPlatformAccess>();
        if (platform is null || !await platform.IsPlatformOperatorAsync(context.HttpContext.RequestAborted))
        {
            context.Result = AccessProblems.PermissionDenied();
        }
    }
}

/// <summary>
/// La acción no exige autenticación (inicio de sesión, renovación, cierre de sesión). Con
/// <see cref="AcceptsAccessToken"/>, acepta un token opcional (aceptar una invitación con una cuenta existente).
/// </summary>
public sealed class AnonymousAccessAttribute : AccessRequirementAttribute
{
    public bool AcceptsAccessToken { get; init; }

    public override bool AcceptsToken => AcceptsAccessToken;

    public override bool RequiresToken => false;

    public override IReadOnlyList<int> ProblemStatusCodes => [];

    public override Task OnAuthorizationAsync(AuthorizationFilterContext context) => Task.CompletedTask;
}

/// <summary>Respuestas de acceso denegado con el formato estándar de errores.</summary>
public static class AccessProblems
{
    public static IActionResult AuthenticationRequired() => Problem(
        StatusCodes.Status401Unauthorized, "Se requiere autenticación", AccessErrorCodes.AuthenticationRequired);

    public static IActionResult OrganizationContextRequired() => Problem(
        StatusCodes.Status403Forbidden, "Se requiere una organización activa", AccessErrorCodes.OrganizationContextRequired);

    public static IActionResult PermissionDenied() => Problem(
        StatusCodes.Status403Forbidden, "No tiene permiso para esta operación", AccessErrorCodes.PermissionDenied);

    private static IActionResult Problem(int status, string title, string code) => new ResultActionResult(Results.Problem(
        statusCode: status,
        title: title,
        extensions: new Dictionary<string, object?> { ["code"] = code }));
}

/// <summary>Ejecuta un <see cref="IResult"/> desde un filtro de MVC (mismo formato que las acciones).</summary>
public sealed class ResultActionResult(IResult result) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context) => result.ExecuteAsync(context.HttpContext);
}
