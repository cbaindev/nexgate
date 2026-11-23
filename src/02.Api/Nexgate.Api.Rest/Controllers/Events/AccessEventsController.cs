using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Controllers.Devices;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Events.Contracts;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Controllers.Events;

internal static class EventsResults
{
    public static IResult ToHttp<T>(EventsResult<T> result, Func<T, IResult> ok) => result.Error switch
    {
        null => ok(result.Value!),
        { Kind: EventsErrorKind.Validation, Errors: { } errors } =>
            Results.ValidationProblem(errors.ToDictionary(e => e.Key, e => e.Value), title: "Error de validación"),
        _ => DevicesResults.NotFound()
    };
}

/// <summary>Eventos de acceso de los sitios de la organización activa, del más reciente al más antiguo.</summary>
[Tags("AccessEvents")]
[Route(Prefix)]
public sealed class AccessEventsController(IAccessEventService events, ICurrentPermissions permissions) : ApiControllerBase
{
    private readonly SiteScope scope = SiteScope.Events(permissions);

    [HttpGet("sites/{siteId:guid}/access-events")]
    [RequirePermission(Permissions.EventsRead)]
    [EndpointName("listAccessEvents")]
    [EndpointSummary("Listar los eventos de acceso de un sitio")]
    [EndpointDescription("Del más reciente al más antiguo por el instante en que ocurrieron, con filtros opcionales por punto, " +
        "sujeto y rango [from, to). Páginas de hasta 100 (50 por defecto); nextCursor pide la siguiente.")]
    [ProducesResponseType<AccessEventPage>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> List(Guid siteId, [FromQuery] Guid? accessPointId, [FromQuery] Guid? subjectId,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? cursor, [FromQuery] int? limit,
        CancellationToken ct) =>
        scope.CanRead(siteId)
            ? EventsResults.ToHttp(await events.ListAsync(new AccessEventQuery(siteId, accessPointId, subjectId, from, to, cursor, limit), ct),
                page => Results.Ok(page))
            : NotFoundProblem();
}
