using Nexgate.Api.Rest.Authorization;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Devices.Contracts;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Controllers.Devices;

internal static class DevicesResults
{
    public static IResult ToHttp<T>(DevicesResult<T> result, Func<T, IResult> ok) =>
        result.Error is not { } error ? ok(result.Value!) : Error(error);

    public static IResult NotFound() => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Recurso no encontrado");

    private static IResult Error(DevicesError error) => error.Kind switch
    {
        DevicesErrorKind.Validation => Results.ValidationProblem(
            new Dictionary<string, string[]> { [error.Field ?? string.Empty] = [error.Message ?? "Valor inválido"] },
            title: "Error de validación",
            extensions: error.Code is null ? null : new Dictionary<string, object?> { ["code"] = error.Code }),
        DevicesErrorKind.Conflict => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code }),
        // Lo inexistente y lo de otra organización responden igual (no revelar existencia).
        _ => NotFound()
    };
}

/// <summary>
/// Alcance de los permisos de dispositivos y eventos (design de add-device-sync-protocol, decisión 8): solo sitios
/// enteros. Fuera de la lectura, el sitio o el recurso no existe (404); visible pero fuera de la gestión, 403.
/// </summary>
internal sealed class SiteScope(ICurrentPermissions permissions, string readPermission, string? managePermission = null)
{
    private readonly PermissionScope read = permissions.ScopeFor(readPermission);
    private readonly PermissionScope manage = managePermission is null ? PermissionScope.None : permissions.ScopeFor(managePermission);

    public static SiteScope Devices(ICurrentPermissions permissions) => new(permissions, Permissions.DevicesRead, Permissions.DevicesManage);

    public static SiteScope Events(ICurrentPermissions permissions) => new(permissions, Permissions.EventsRead);

    public bool CanRead(Guid siteId) => read.CoversSite(siteId);

    /// <summary>Null si puede gestionar el sitio; si no, la respuesta que corresponde.</summary>
    public IResult? DenyManage(Guid siteId) =>
        manage.CoversSite(siteId) ? null : CanRead(siteId) ? AccessProblems.PermissionDenied() : DevicesResults.NotFound();
}
