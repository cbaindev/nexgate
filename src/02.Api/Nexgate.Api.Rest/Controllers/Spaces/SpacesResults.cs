using Nexgate.Api.Rest.Authorization;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Spaces.Contracts;

namespace Nexgate.Api.Rest.Controllers.Spaces;

internal static class SpacesResults
{
    public const string MissingParentMessage = "El padre no existe en el sitio.";
    public const string MissingSpaceMessage = "El espacio no existe en el sitio.";

    public static IResult ToHttp<T>(SpacesResult<T> result, Func<T, IResult> ok) => result switch
    {
        { Error: null } => ok(result.Value!),
        { Error.Kind: SpacesErrorKind.Validation } => Validation(result.Error.Field ?? string.Empty, result.Error.Message ?? "Valor inválido"),
        { Error.Kind: SpacesErrorKind.Conflict } => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: result.Error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code }),
        // Lo inexistente y lo de otra organización responden igual (no revelar existencia).
        _ => NotFound()
    };

    public static IResult NotFound() => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Recurso no encontrado");

    /// <summary>Mismo formato que los errores de validación del servicio (un campo fuera del alcance responde como inexistente).</summary>
    public static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }, title: "Error de validación");
}

/// <summary>
/// Alcance de los permisos de Spaces (design de add-space-scoped-roles, decisión 4). Un espacio está cubierto si el
/// permiso cubre su sitio entero o alguno de los espacios de su camino; un punto de acceso, si está cubierto el espacio al
/// que da acceso. Fuera de la lectura, el recurso no existe (404); visible pero fuera de la gestión, 403.
/// </summary>
internal sealed class SpacesScope(ICurrentPermissions permissions)
{
    public PermissionScope Read { get; } = permissions.ScopeFor(Permissions.SpacesRead);

    public PermissionScope Manage { get; } = permissions.ScopeFor(Permissions.SpacesManage);

    public bool CanRead(SpaceDto space) => Read.Covers(space.SiteId, space.PathIds());

    public bool CanManage(SpaceDto space) => Manage.Covers(space.SiteId, space.PathIds());

    /// <summary>Null si puede gestionar el espacio; si no, la respuesta que corresponde.</summary>
    public IResult? DenyManage(SpaceDto space) =>
        CanManage(space) ? null : CanRead(space) ? AccessProblems.PermissionDenied() : SpacesResults.NotFound();

    /// <summary>
    /// Null si puede gestionar algo del sitio (el sitio entero o alguno de sus espacios); si no, 403 si lo ve y 404 si no.
    /// </summary>
    public IResult? DenyManageInSite(Guid siteId) =>
        Manage.ReachesSite(siteId)
            ? null
            : Read.ReachesSite(siteId) ? AccessProblems.PermissionDenied() : SpacesResults.NotFound();
}
