using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Middlewares;
using Nexgate.Api.Rest.Tenancy;
using Nexgate.Common.BuildingBlocks.Tenancy;

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
        services.AddScoped<ICurrentOrganization, HttpContextCurrentOrganization>();

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

                problem.Extensions["code"] = ErrorCodeFor(problem);
                problem.Extensions["correlationId"] = context.HttpContext.GetCorrelationId();
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    /// <summary>Orden del pipeline: correlación → manejo de errores → páginas de estado → enriquecimiento de telemetría.</summary>
    public static IApplicationBuilder UseNexgatePipeline(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseMiddleware<TelemetryEnrichmentMiddleware>();
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
