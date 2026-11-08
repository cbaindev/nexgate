using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexgate.BuildingBlocks.Access;
using Nexgate.BuildingBlocks.Correlation;

namespace Nexgate.BuildingBlocks.Errors;

public static class ProblemDetailsSetup
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
        services.AddNexgateRequestContext();
        // Los esquemas de autenticación y las políticas de límite los agrega Identity.
        services.AddAuthentication();
        services.AddRateLimiter(_ => { });

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var problem = context.ProblemDetails;
                // Un endpoint puede fijar un código más específico; si no, se deriva del estado.
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
        app.UseMiddleware<Telemetry.TelemetryEnrichmentMiddleware>();
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

/// <summary>Convierte excepciones no controladas en un 500 sin detalles internos.</summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Excepción no controlada");

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno del servidor"
            }
        });
    }
}
