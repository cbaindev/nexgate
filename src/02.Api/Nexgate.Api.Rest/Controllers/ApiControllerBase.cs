using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Nexgate.Api.Rest.Controllers;

/// <summary>
/// Base de los controllers de negocio: validación automática de <c>[ApiController]</c>, versión 1 bajo
/// <c>api/v{version}</c> (cada controller declara su ruta con <see cref="Prefix"/>: ASP.NET Core no combina rutas heredadas) y solo JSON de entrada y salida, con los mismos tipos de contenido que declara el contrato
/// OpenAPI. Las acciones devuelven <see cref="IResult"/> para escribir respuestas y errores igual en todos los módulos.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Prefijo de versión de todos los endpoints de negocio.</summary>
    public const string Prefix = "api/v{version:apiVersion}/";

    /// <summary>Lo inexistente y lo de otra organización responden igual (no revelar existencia).</summary>
    protected static IResult NotFoundProblem() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Recurso no encontrado");

    protected static IResult ValidationProblem(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }, title: "Error de validación");

    protected static IResult Problem(int status, string? title, string code) =>
        Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });
}
