using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Access;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Access.Contracts;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Controllers.Access;

/// <summary>
/// Otorgamientos (D-13): sujeto → destino (espacio o punto) → horario → vigencia → efecto. Un otorgamiento a un espacio
/// cubre las puertas del camino a ese espacio; gana siempre la denegación.
/// </summary>
[Tags("Grants")]
[Route(Prefix)]
public sealed class GrantsController(IGrantService grants, ICurrentPermissions permissions, RequestContext context) : ApiControllerBase
{
    private readonly AccessScope scope = new(permissions);

    [HttpGet("sites/{siteId:guid}/grants")]
    [RequirePermission(Permissions.AccessRead)]
    [EndpointName("listGrants")]
    [EndpointSummary("Listar los otorgamientos de un sitio")]
    [EndpointDescription("Opcionalmente, solo los de un sujeto.")]
    [ProducesResponseType<List<GrantDto>>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> List(Guid siteId, [FromQuery] Guid? subjectId, CancellationToken ct) =>
        scope.CanRead(siteId) ? AccessResults.ToHttp(await grants.ListAsync(siteId, subjectId, ct), list => Results.Ok(list)) : NotFoundProblem();

    [HttpPost("sites/{siteId:guid}/grants")]
    [RequirePermission(Permissions.AccessManage)]
    [EndpointName("createGrant")]
    [EndpointSummary("Crear un otorgamiento")]
    [EndpointDescription("Sujeto, destino y horario tienen que ser del sitio. El máximo de usos solo vale para permisos.")]
    [ProducesResponseType<GrantDto>(StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Create(Guid siteId, CreateGrantRequest request, CancellationToken ct) =>
        scope.DenyManage(siteId) ?? AccessResults.ToHttp(
            await grants.CreateAsync(siteId, new GrantInput(request.SubjectId!.Value, request.TargetType!, request.TargetId!.Value,
                request.ScheduleId, request.ValidFrom, request.ValidUntil, request.Effect!, request.MaxUses), context.UserId!.Value, ct),
            grant => Results.Created($"/api/v1/grants/{grant.Id}", grant));

    [HttpGet("grants/{id:guid}")]
    [RequirePermission(Permissions.AccessRead)]
    [EndpointName("getGrant")]
    [EndpointSummary("Obtener un otorgamiento")]
    [ProducesResponseType<GrantDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is { } grant ? Results.Ok(grant) : NotFoundProblem();

    [HttpDelete("grants/{id:guid}")]
    [RequirePermission(Permissions.AccessManage)]
    [EndpointName("deleteGrant")]
    [EndpointSummary("Eliminar un otorgamiento")]
    [EndpointDescription("Los otorgamientos no se modifican: se eliminan y se crea otro.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is not { } grant
            ? NotFoundProblem()
            : scope.DenyManage(grant.SiteId) ?? AccessResults.ToHttp(await grants.DeleteAsync(id, ct), _ => Results.NoContent());

    private async Task<GrantDto?> VisibleAsync(Guid id, CancellationToken ct) =>
        (await grants.GetAsync(id, ct)).Value is { } grant && scope.CanRead(grant.SiteId) ? grant : null;
}
