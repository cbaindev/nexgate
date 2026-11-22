using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Spaces;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Spaces.Contracts;

namespace Nexgate.Api.Rest.Controllers.Spaces;

/// <summary>
/// Puntos de acceso de los sitios de la organización activa. Usan los mismos permisos que los espacios: un punto está en
/// el alcance si lo está el espacio al que da acceso.
/// </summary>
[Tags("AccessPoints")]
[Route(Prefix)]
public sealed class AccessPointsController(IAccessPointService points, ISpaceService spaces, ICurrentPermissions permissions)
    : ApiControllerBase
{
    private readonly SpacesScope scope = new(permissions);

    [HttpGet("sites/{siteId:guid}/access-points")]
    [RequirePermission(Permissions.SpacesRead)]
    [EndpointName("listAccessPoints")]
    [EndpointSummary("Listar los puntos de acceso de un sitio")]
    [EndpointDescription("Ordenados por nombre. Con alcance de espacio, solo los que dan acceso a ese espacio o a sus descendientes.")]
    [ProducesResponseType<List<AccessPointDto>>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> List(Guid siteId, CancellationToken ct)
    {
        if (!scope.Read.ReachesSite(siteId))
        {
            return NotFoundProblem();
        }

        var result = await points.ListAsync(siteId, ct);
        if (result.Error is not null || scope.Read.CoversSite(siteId))
        {
            return SpacesResults.ToHttp(result, list => Results.Ok(list));
        }

        var visible = ((await spaces.ListAsync(siteId, ct)).Value ?? []).Where(scope.CanRead).Select(s => s.Id).ToHashSet();
        return Results.Ok(result.Value!.Where(p => visible.Contains(p.ToSpaceId)).ToList());
    }

    [HttpPost("sites/{siteId:guid}/access-points")]
    [RequirePermission(Permissions.SpacesManage)]
    [EndpointName("createAccessPoint")]
    [EndpointSummary("Crear un punto de acceso")]
    [EndpointDescription("El espacio al que da acceso y el de origen deben ser del mismo sitio; el modo de fallo es obligatorio. " +
        "Con alcance de espacio, el espacio al que da acceso tiene que estar en el subárbol del alcance.")]
    [ProducesResponseType<AccessPointDto>(StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create(Guid siteId, AccessPointRequest request, CancellationToken ct)
    {
        if (scope.DenyManageInSite(siteId) is { } denied)
        {
            return denied;
        }

        if (await DenyTargetAsync(siteId, request.ToSpaceId!.Value, ct) is { } invalid)
        {
            return invalid;
        }

        return SpacesResults.ToHttp(await points.CreateAsync(siteId, Input(request), ct),
            point => Results.Created($"/api/v1/access-points/{point.Id}", point));
    }

    [HttpGet("access-points/{id:guid}")]
    [RequirePermission(Permissions.SpacesRead)]
    [EndpointName("getAccessPoint")]
    [EndpointSummary("Obtener un punto de acceso")]
    [EndpointDescription("Un punto fuera del alcance responde igual que uno inexistente (404).")]
    [ProducesResponseType<AccessPointDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid id, CancellationToken ct) =>
        await FindAsync(id, ct) is { Readable: true } found ? Results.Ok(found.Point) : NotFoundProblem();

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
        if (await FindAsync(id, ct) is not { Readable: true } found)
        {
            return NotFoundProblem();
        }

        if (!found.Manageable)
        {
            return AccessProblems.PermissionDenied();
        }

        if (await DenyTargetAsync(found.Point.SiteId, request.ToSpaceId!.Value, ct) is { } invalid)
        {
            return invalid;
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
        if (await FindAsync(id, ct) is not { Readable: true } found)
        {
            return NotFoundProblem();
        }

        if (!found.Manageable)
        {
            return AccessProblems.PermissionDenied();
        }

        return SpacesResults.ToHttp(await points.DeleteAsync(id, ct), _ => Results.NoContent());
    }

    private static AccessPointInput Input(AccessPointRequest request) =>
        new(request.Name!, request.Type!, request.ToSpaceId!.Value, request.FromSpaceId, request.FailMode!);

    private sealed record Found(AccessPointDto Point, bool Readable, bool Manageable);

    /// <summary>El punto y si está en el alcance de lectura y de gestión (por el espacio al que da acceso).</summary>
    private async Task<Found?> FindAsync(Guid id, CancellationToken ct)
    {
        if ((await points.GetAsync(id, ct)).Value is not { } point)
        {
            return null;
        }

        if (scope.Read.CoversSite(point.SiteId) && scope.Manage.CoversSite(point.SiteId))
        {
            return new Found(point, true, true);
        }

        var target = (await spaces.GetAsync(point.ToSpaceId, ct)).Value;
        return target is null
            ? new Found(point, scope.Read.CoversSite(point.SiteId), scope.Manage.CoversSite(point.SiteId))
            : new Found(point, scope.CanRead(target), scope.CanManage(target));
    }

    /// <summary>
    /// Con solo alcance de espacio, el espacio al que da acceso tiene que estar en el subárbol: si no, responde como un
    /// espacio inexistente (400), sin revelar ramas que el llamador no ve.
    /// </summary>
    private async Task<IResult?> DenyTargetAsync(Guid siteId, Guid toSpaceId, CancellationToken ct)
    {
        if (scope.Manage.CoversSite(siteId))
        {
            return null;
        }

        return (await spaces.GetAsync(toSpaceId, ct)).Value is { } target && target.SiteId == siteId && scope.CanManage(target)
            ? null
            : SpacesResults.Validation(nameof(AccessPointRequest.ToSpaceId), SpacesResults.MissingSpaceMessage);
    }
}
