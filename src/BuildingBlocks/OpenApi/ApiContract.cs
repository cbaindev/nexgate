using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Nexgate.BuildingBlocks.OpenApi;

/// <summary>
/// El endpoint acepta el token de acceso. Si <see cref="Required"/> es falso, el token es opcional (por ejemplo,
/// aceptar una invitación con una cuenta existente). El documento OpenAPI lo traduce al requisito de seguridad.
/// </summary>
public sealed record AccessTokenMetadata(bool Required);

/// <summary>
/// Declaración de respuestas de error para el contrato OpenAPI; las de éxito usan <c>Produces</c> (design de
/// add-openapi-contract, decisión 2). Los errores usan siempre el formato estándar:<see cref="HttpValidationProblemDetails"/> para 400 y
/// <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> para el resto.
/// </summary>
public static class ApiContractExtensions
{
    private static readonly string[] ProblemContentTypes = ["application/problem+json"];

    public static TBuilder AcceptsAccessToken<TBuilder>(this TBuilder builder, bool required = true)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new AccessTokenMetadata(required));

    /// <summary>Respuestas de error con el formato estándar.</summary>
    public static TBuilder Problems<TBuilder>(this TBuilder builder, params int[] statusCodes) where TBuilder : IEndpointConventionBuilder
    {
        foreach (var statusCode in statusCodes)
        {
            var type = statusCode == StatusCodes.Status400BadRequest
                ? typeof(HttpValidationProblemDetails)
                : typeof(Microsoft.AspNetCore.Mvc.ProblemDetails);
            builder.WithMetadata(new ProducesResponseTypeMetadata(statusCode, type, ProblemContentTypes));
        }

        return builder;
    }
}
