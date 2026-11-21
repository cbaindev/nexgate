using Microsoft.AspNetCore.Mvc;

namespace Nexgate.Api.Rest.Controllers;

/// <summary>
/// Base de los controllers de negocio: validación automática de <c>[ApiController]</c> y solo JSON de entrada y
/// salida, con los mismos tipos de contenido que declara el contrato OpenAPI.
/// </summary>
[ApiController]
[Consumes("application/json")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase;
