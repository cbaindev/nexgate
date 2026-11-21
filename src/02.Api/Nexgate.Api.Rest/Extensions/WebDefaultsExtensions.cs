using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Middlewares;
using Nexgate.Common.BuildingBlocks.Security;

namespace Nexgate.Api.Rest.Extensions;

public static class WebDefaultsExtensions
{
    /// <summary>
    /// Todo error se devuelve como problem details (RFC 9457) con código estable, estado HTTP
    /// y el identificador de correlación.
    /// </summary>
    public static IServiceCollection AddNexgateWebDefaults(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        // Números estrictos en JSON: "5" no vale como 5. Así el contrato OpenAPI declara enteros y no "entero o texto".
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
        services.Configure<JsonOptions>(options => options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict);
        services.AddNexgateRequestContext();
        // Los esquemas de autenticación y las políticas de límite se configuran en AuthenticationExtensions.
        services.AddAuthentication();
        services.AddRateLimiter(_ => { });

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var problem = context.ProblemDetails;
                if (problem is HttpValidationProblemDetails)
                {
                    // El 400 automático de [ApiController] trae un título genérico en inglés.
                    problem.Title = "Error de validación";
                }

                // Una acción o un filtro puede fijar un código más específico; si no, se deriva del estado.
                problem.Extensions.TryAdd("code", ErrorCodeFor(problem));
                problem.Extensions["correlationId"] = context.HttpContext.GetCorrelationId();
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    /// <summary>
    /// Orden del pipeline: correlación → manejo de errores → páginas de estado → autenticación (token) →
    /// contexto de la solicitud (usuario, organización activa y permisos) → enriquecimiento de telemetría →
    /// límite de intentos.
    /// </summary>
    public static IApplicationBuilder UseNexgatePipeline(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseMiddleware<RequestContextMiddleware>();
        app.UseMiddleware<TelemetryEnrichmentMiddleware>();
        app.UseRateLimiter();
        return app;
    }

    private static string ErrorCodeFor(ProblemDetails problem) => problem switch
    {
        HttpValidationProblemDetails => "validation_error",
        _ => problem.Status switch
        {
            400 => "bad_request",
            401 => "unauthorized",
            403 => "forbidden",
            404 => "not_found",
            405 => "method_not_allowed",
            500 => "internal_error",
            503 => "service_unavailable",
            var status => $"http_{status}"
        }
    };
}
