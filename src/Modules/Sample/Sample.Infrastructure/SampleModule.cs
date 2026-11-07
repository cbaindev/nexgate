using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexgate.BuildingBlocks.Validation;
using Nexgate.Modules.Sample.Application;
using Nexgate.Modules.Sample.Contracts;

namespace Nexgate.Modules.Sample.Infrastructure;

public sealed record EchoRequest(
    [property: Required(ErrorMessage = "El nombre es obligatorio")]
    [property: StringLength(50, ErrorMessage = "El nombre no puede superar 50 caracteres")]
    string? Name);

/// <summary>
/// Módulo de ejemplo. Existe solo para demostrar la estructura de un módulo y probar las convenciones
/// de la API; se elimina cuando llegue el primer módulo real.
/// </summary>
public static class SampleModule
{
    public static IServiceCollection AddSampleModule(this IServiceCollection services)
    {
        services.AddSingleton<IGreetingService, GreetingService>();
        return services;
    }

    public static IEndpointRouteBuilder MapSampleEndpoints(this IEndpointRouteBuilder api, IConfiguration configuration)
    {
        var group = api.MapGroup("/sample");

        group.MapGet("/ping", (ILoggerFactory loggerFactory) =>
        {
            loggerFactory.CreateLogger("Nexgate.Sample").LogInformation("Ping recibido");
            return Results.Ok(new { status = "ok" });
        });

        group.MapPost("/greet", (EchoRequest request, IGreetingService greetings) =>
                Results.Ok(greetings.Greet(request.Name!)))
            .WithValidation<EchoRequest>();

        // Solo para probar el manejo de errores 500. Deshabilitado salvo que se active por configuración.
        if (configuration.GetValue<bool>("Sample:EnableFailureEndpoint"))
        {
            group.MapGet("/fail", () => { throw new InvalidOperationException("detalle-interno-secreto"); });
        }

        return group;
    }
}
