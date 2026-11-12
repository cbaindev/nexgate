using Asp.Versioning;

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
        services.AddControllers();
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = false;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
                options.UnsupportedApiVersionStatusCode = StatusCodes.Status404NotFound;
            })
            .AddMvc();
        return services;
    }
}
