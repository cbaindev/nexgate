using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Extensions.OpenApi;

namespace Nexgate.Api.Rest.Extensions;

public static class ApiVersioningExtensions
{
    /// <summary>
    /// Versionado por segmento de URL (api/v1/...). Una ruta sin versión no matchea ningún controller (404) y una
    /// versión no soportada también responde 404, para que todo lo que esté fuera de una versión válida se
    /// comporte igual.
    /// </summary>
    public static IServiceCollection AddNexgateControllers(this IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            options.Conventions.Add(new ApiResponseConvention());
            // Las acciones devuelven IResult (siempre application/json); los formateadores de salida solo describen las
            // respuestas en el explorador de la API.
            options.OutputFormatters.RemoveType<StringOutputFormatter>();
            foreach (var formatter in options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>())
            {
                formatter.SupportedMediaTypes.Clear();
                formatter.SupportedMediaTypes.Add("application/json");
            }
        });
        // El 400 automático de [ApiController] se escribe como cualquier otro error (problem details con code y
        // correlationId), sin depender de los formateadores de MVC.
        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
            new ResultActionResult(Results.ValidationProblem(
                context.ModelState
                    .Where(entry => entry.Value is { Errors.Count: > 0 })
                    .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors
                        .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Valor inválido" : e.ErrorMessage)
                        .ToArray()),
                title: "Error de validación")));
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = false;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
                options.UnsupportedApiVersionStatusCode = StatusCodes.Status404NotFound;
            })
            .AddMvc()
            // El documento OpenAPI muestra las rutas con la versión ya sustituida (/api/v1/...).
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });
        return services;
    }
}
