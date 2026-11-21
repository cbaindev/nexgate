using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Controllers;

namespace Nexgate.Api.Rest.Extensions.OpenApi;

/// <summary>
/// Declara en cada acción de negocio las respuestas de error que no dependen de la acción, para el contrato OpenAPI
/// (design de integrate-modules-into-layers, D7): 500 en todas, 400 en las que reciben cuerpo, 401/403 según su
/// requisito de acceso y 429 en las que tienen límite de intentos. Las respuestas propias (404, 409, ...) las declara
/// cada acción con <see cref="ProblemResponseAttribute"/>.
/// </summary>
public sealed class ApiResponseConvention : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        if (!action.Controller.ControllerType.IsAssignableTo(typeof(ApiControllerBase)))
        {
            return;
        }

        var declared = action.Attributes.Concat(action.Controller.Attributes)
            .OfType<ProducesResponseTypeAttribute>()
            .Select(a => a.StatusCode)
            .ToHashSet();

        var statusCodes = new List<int> { StatusCodes.Status500InternalServerError };
        if (action.Parameters.Any(p => p.BindingInfo?.BindingSource == BindingSource.Body))
        {
            statusCodes.Add(StatusCodes.Status400BadRequest);
        }

        var access = action.Attributes.OfType<AccessRequirementAttribute>().LastOrDefault()
            ?? action.Controller.Attributes.OfType<AccessRequirementAttribute>().LastOrDefault();
        if (access is not null)
        {
            statusCodes.AddRange(access.ProblemStatusCodes);
        }

        if (action.Attributes.OfType<EnableRateLimitingAttribute>().Any())
        {
            statusCodes.Add(StatusCodes.Status429TooManyRequests);
        }

        foreach (var statusCode in statusCodes.Where(declared.Add))
        {
            var response = new ProblemResponseAttribute(statusCode);
            // Los filtros alimentan el explorador de la API (y el documento); los metadatos del endpoint, la
            // verificación de respuestas declaradas de las pruebas.
            action.Filters.Add(response);
            foreach (var selector in action.Selectors)
            {
                selector.EndpointMetadata.Add(response);
            }
        }
    }
}
