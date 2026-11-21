using Microsoft.AspNetCore.Mvc;

namespace Nexgate.Api.Rest.Controllers;

/// <summary>
/// Respuesta de error con el formato estándar, para el contrato OpenAPI: <see cref="HttpValidationProblemDetails"/>
/// para 400 y <see cref="ProblemDetails"/> para el resto.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProblemResponseAttribute(int statusCode) : ProducesResponseTypeAttribute(
    statusCode == StatusCodes.Status400BadRequest ? typeof(HttpValidationProblemDetails) : typeof(ProblemDetails),
    statusCode,
    ProblemContentType)
{
    public const string ProblemContentType = "application/problem+json";
}
