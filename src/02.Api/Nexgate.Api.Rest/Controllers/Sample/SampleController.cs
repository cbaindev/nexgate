using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Nexgate.Service.Sample.Contracts;

namespace Nexgate.Api.Rest.Controllers.Sample;

/// <summary>
/// Controller del módulo de ejemplo. Existe solo para probar las convenciones de la API; se elimina cuando
/// llegue el primer módulo real.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/sample")]
public sealed class SampleController(
    IGreetingService greetings,
    IConfiguration configuration,
    ILogger<SampleController> logger) : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        logger.LogInformation("Ping recibido");
        return Ok(new { status = "ok" });
    }

    [HttpPost("greet")]
    public ActionResult<GreetingDto> Greet(GreetRequest request) => greetings.Greet(request.Name!);

    /// <summary>Solo para probar el manejo de errores 500. Responde 404 salvo que se active por configuración.</summary>
    [HttpGet("fail")]
    public IActionResult Fail()
    {
        if (!configuration.GetValue<bool>("Sample:EnableFailureEndpoint"))
        {
            return NotFound();
        }

        throw new InvalidOperationException("detalle-interno-secreto");
    }
}
