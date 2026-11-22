using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Spaces.Contracts;
using Nexgate.Api.Rest.Authorization;

namespace Nexgate.Api.Rest.Controllers.Spaces;

internal static class SpacesResults
{
    public static IResult ToHttp<T>(SpacesResult<T> result, Func<T, IResult> ok) => result switch
    {
        { Error: null } => ok(result.Value!),
        { Error.Kind: SpacesErrorKind.Validation } => Results.ValidationProblem(
            new Dictionary<string, string[]> { [result.Error.Field ?? string.Empty] = [result.Error.Message ?? "Valor inválido"] },
            title: "Error de validación"),
        { Error.Kind: SpacesErrorKind.Conflict } => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: result.Error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code }),
        // Lo inexistente y lo de otra organización responden igual (no revelar existencia).
        _ => NotFound()
    };

    public static IResult NotFound() => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Recurso no encontrado");
}

/// <summary>
/// Alcance por sitio de los permisos de Spaces (design, decisión 5): fuera del alcance de lectura, el sitio o el
/// recurso no existe (404); visible pero fuera del alcance de gestión, 403.
/// </summary>
internal static class SpacesScope
{
    public static bool CanRead(ICurrentPermissions permissions, Guid siteId) =>
        permissions.SitesFor(Permissions.SpacesRead).Allows(siteId);

    /// <summary>Null si puede gestionar el sitio; si no, la respuesta que corresponde.</summary>
    public static IResult? DenyManage(ICurrentPermissions permissions, Guid siteId) =>
        permissions.SitesFor(Permissions.SpacesManage).Allows(siteId)
            ? null
            : CanRead(permissions, siteId) ? AccessProblems.PermissionDenied() : SpacesResults.NotFound();
}
