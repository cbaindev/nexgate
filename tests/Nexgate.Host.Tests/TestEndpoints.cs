using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Nexgate.Host.Tests;

/// <summary>
/// Endpoints que existen solo en las pruebas, para verificar convenciones transversales (errores 500, correlación,
/// telemetría) sin agregar código de prueba al producto. Se registran al final del pipeline real.
/// </summary>
public sealed class TestEndpoints : IStartupFilter
{
    public const string Ping = "/api/v1/test/ping";
    public const string Fail = "/api/v1/test/fail";
    public const string PingLogMessage = "Ping recibido";
    public const string SecretFailureDetail = "detalle-interno-secreto";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);

        app.Map(Ping, branch => branch.Run(async context =>
        {
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Nexgate.Tests").LogInformation(PingLogMessage);
            await context.Response.WriteAsJsonAsync(new { status = "ok" });
        }));

        app.Map(Fail, branch => branch.Run(_ => throw new InvalidOperationException(SecretFailureDetail)));
    };
}
