using Nexgate.Api.Rest.Authorization;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Access.Contracts;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Occupancy.Contracts;

namespace Nexgate.Api.Rest.Controllers.Access;

internal static class AccessResults
{
    public static IResult ToHttp<T>(AccessResult<T> result, Func<T, IResult> ok) =>
        result.Error is not { } error ? ok(result.Value!) : Error(error.Kind.ToString(), error.Field, error.Message, error.Code);

    public static IResult ToHttp<T>(OccupancyResult<T> result, Func<T, IResult> ok) =>
        result.Error is not { } error ? ok(result.Value!) : Error(error.Kind.ToString(), error.Field, error.Message, error.Code);

    public static IResult NotFound() => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Recurso no encontrado");

    private static IResult Error(string kind, string? field, string? message, string? code) => kind switch
    {
        "Validation" => Results.ValidationProblem(
            new Dictionary<string, string[]> { [field ?? string.Empty] = [message ?? "Valor inválido"] }, title: "Error de validación"),
        "Conflict" => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: message,
            extensions: new Dictionary<string, object?> { ["code"] = code }),
        // Lo inexistente y lo de otra organización responden igual (no revelar existencia).
        _ => NotFound()
    };
}

/// <summary>
/// Alcance de los permisos de Access (design de add-access-evaluator, decisión 9): solo sitios enteros. Fuera de la
/// lectura, el sitio o el recurso no existe (404); visible pero fuera de la gestión, 403.
/// </summary>
internal sealed class AccessScope(ICurrentPermissions permissions)
{
    private readonly PermissionScope read = permissions.ScopeFor(Permissions.AccessRead);
    private readonly PermissionScope manage = permissions.ScopeFor(Permissions.AccessManage);

    public bool CanRead(Guid siteId) => read.CoversSite(siteId);

    /// <summary>Null si puede gestionar el sitio; si no, la respuesta que corresponde.</summary>
    public IResult? DenyManage(Guid siteId) =>
        manage.CoversSite(siteId) ? null : CanRead(siteId) ? AccessProblems.PermissionDenied() : AccessResults.NotFound();
}
