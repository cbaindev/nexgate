using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Spaces;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Spaces.Contracts;

namespace Nexgate.Api.Rest.Controllers.Spaces;

/// <summary>
/// Puntos de acceso de los sitios de la organización activa. Usan los mismos permisos y alcance por sitio que los
/// espacios.
/// </summary>
[Tags("AccessPoints")]
[Route(Prefix)]
public sealed class AccessPointsController(IAccessPointService points, ICurrentPermissions permissions) : ApiControllerBase
{
    [HttpGet("sites/{siteId:guid}/access-points")]
    [RequirePermission(Permissions.SpacesRead)]
    [EndpointName("listAccessPoints")]
    [EndpointSummary("Listar los puntos de acceso de un sitio")]
    [EndpointDescription("Ordenados por nombre.")]
    [ProducesResponseType<List<AccessPointDto>>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> List(Guid siteId, CancellationToken ct) =>
        SpacesScope.CanRead(permissions, siteId)
            ? SpacesResults.ToHttp(await points.ListAsync(siteId, ct), list => Results.Ok(list))
            : NotFoundProblem();

    [HttpPost("sites/{siteId:guid}/access-points")]
    [RequirePermission(Permissions.SpacesManage)]
    [EndpointName("createAccessPoint")]
    [EndpointSummary("Crear un punto de acceso")]
    [EndpointDescription("El espacio al que da acceso y el de origen deben ser del mismo sitio; el modo de fallo es obligatorio.")]
    [ProducesResponseType<AccessPointDto>(StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create(Guid siteId, AccessPointRequest request, CancellationToken ct)
    {
        if (SpacesScope.DenyManage(permissions, siteId) is { } denied)
        {
            return denied;
        }

        return SpacesResults.ToHttp(await points.CreateAsync(siteId, Input(request), ct),
            point => Results.Created($"/api/v1/access-points/{point.Id}", point));
    }

    [HttpGet("access-points/{id:guid}")]
    [RequirePermission(Permissions.SpacesRead)]
    [EndpointName("getAccessPoint")]
    [EndpointSummary("Obtener un punto de acceso")]
    [EndpointDescription("Un punto de un sitio fuera del alcance responde igual que uno inexistente (404).")]
    [ProducesResponseType<AccessPointDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is { } point ? Results.Ok(point) : NotFoundProblem();

    [HttpPut("access-points/{id:guid}")]
    [RequirePermission(Permissions.SpacesManage)]
    [EndpointName("updateAccessPoint")]
    [EndpointSummary("Modificar un punto de acceso")]
    [EndpointDescription("Cambia todos sus atributos salvo el sitio.")]
    [ProducesResponseType<AccessPointDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Update(Guid id, AccessPointRequest request, CancellationToken ct)
    {
        if (await VisibleAsync(id, ct) is not { } point)
        {
            return NotFoundProblem();
        }

        if (SpacesScope.DenyManage(permissions, point.SiteId) is { } denied)
        {
            return denied;
        }

        return SpacesResults.ToHttp(await points.UpdateAsync(id, Input(request), ct), Results.Ok);
    }

    [HttpDelete("access-points/{id:guid}")]
    [RequirePermission(Permissions.SpacesManage)]
    [EndpointName("deleteAccessPoint")]
    [EndpointSummary("Eliminar un punto de acceso")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(Guid id, CancellationToken ct)
    {
        if (await VisibleAsync(id, ct) is not { } point)
        {
            return NotFoundProblem();
        }

        if (SpacesScope.DenyManage(permissions, point.SiteId) is { } denied)
        {
            return denied;
        }

        return SpacesResults.ToHttp(await points.DeleteAsync(id, ct), _ => Results.NoContent());
    }

    private static AccessPointInput Input(AccessPointRequest request) =>
        new(request.Name!, request.Type!, request.ToSpaceId!.Value, request.FromSpaceId, request.FailMode!);

    /// <summary>El punto, si existe en la organización activa y su sitio está en el alcance de lectura.</summary>
    private async Task<AccessPointDto?> VisibleAsync(Guid id, CancellationToken ct) =>
        (await points.GetAsync(id, ct)).Value is { } point && SpacesScope.CanRead(permissions, point.SiteId) ? point : null;
}
