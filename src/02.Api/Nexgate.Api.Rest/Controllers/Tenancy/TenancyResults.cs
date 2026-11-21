using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Api.Rest.Controllers.Tenancy;

internal static class TenancyResults
{
    public static IResult ToHttp<T>(TenancyResult<T> result, Func<T, IResult> ok) => result switch
    {
        { Error: null, Value: { } value } => ok(value),
        { Error.Kind: TenancyErrorKind.Validation } => Results.ValidationProblem(
            new Dictionary<string, string[]> { [result.Error.Field ?? string.Empty] = [result.Error.Message ?? "Valor inválido"] },
            title: "Error de validación"),
        { Error.Kind: TenancyErrorKind.Conflict } => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: result.Error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = TenancyErrorCodes.SiteNameConflict }),
        // Lo inexistente y lo de otra organización responden igual (no revelar existencia).
        _ => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Recurso no encontrado")
    };
}
