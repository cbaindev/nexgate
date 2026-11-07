using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexgate.BuildingBlocks.Correlation;
using Nexgate.BuildingBlocks.Tenancy;

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
        services.AddScoped<ICurrentOrganization, HttpContextCurrentOrganization>();
        services.AddNexgateTenancy();

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
    /// Orden del pipeline: correlación → manejo de errores → páginas de estado → organización activa →
    /// enriquecimiento de telemetría. La autenticación (cuando exista) va antes de la organización activa.
    /// </summary>
    public static IApplicationBuilder UseNexgatePipeline(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseMiddleware<OrganizationResolutionMiddleware>();
        app.UseMiddleware<Telemetry.TelemetryEnrichmentMiddleware>();
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
