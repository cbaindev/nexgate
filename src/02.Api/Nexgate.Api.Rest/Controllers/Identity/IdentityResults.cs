using Nexgate.Api.Rest.Authorization;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Controllers.Identity;

internal static class IdentityResults
{
    public static IResult ToHttp<T>(IdentityResult<T> result, Func<T, IResult> ok) => result switch
    {
        { Error: null, Value: { } value } => ok(value),
        { Error: { } error } => ToHttp(error),
        _ => throw new InvalidOperationException("Resultado sin valor ni error.")
    };

    public static IResult ToHttp(IdentityError error) => error.Kind switch
    {
        IdentityErrorKind.Validation => Validation(error.Field ?? string.Empty, error.Message ?? "Valor inválido"),
        IdentityErrorKind.InvalidCredentials => Problem(StatusCodes.Status401Unauthorized, "Credenciales inválidas", IdentityErrorCodes.InvalidCredentials),
        IdentityErrorKind.InvalidRefreshToken => Problem(StatusCodes.Status401Unauthorized, "Token de renovación inválido", IdentityErrorCodes.InvalidRefreshToken),
        IdentityErrorKind.OrganizationNotAvailable => Problem(StatusCodes.Status403Forbidden, "La organización no está disponible para este usuario",
            IdentityErrorCodes.OrganizationNotAvailable),
        IdentityErrorKind.InvalidInvitation => Problem(StatusCodes.Status400BadRequest, "Invitación inválida, vencida o ya usada",
            IdentityErrorCodes.InvalidInvitation),
        IdentityErrorKind.AuthenticationRequired => AccessProblems.AuthenticationRequired(),
        IdentityErrorKind.Conflict => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: error.Message),
        _ => NotFound()
    };

    public static IResult ToHttp(RemovalOutcome outcome) => outcome switch
    {
        RemovalOutcome.Removed => Results.NoContent(),
        RemovalOutcome.LastAdministrator => Problem(StatusCodes.Status409Conflict,
            "La organización debe conservar al menos un administrador", IdentityErrorCodes.LastAdministrator),
        _ => NotFound()
    };

    public static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }, title: "Error de validación");

    // Lo inexistente y lo de otra organización responden igual (no revelar existencia).
    public static IResult NotFound() => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Recurso no encontrado");

    private static IResult Problem(int status, string title, string code) =>
        Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });
}
