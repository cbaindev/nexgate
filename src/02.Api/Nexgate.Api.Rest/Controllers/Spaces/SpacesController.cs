using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Spaces;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Spaces.Contracts;

namespace Nexgate.Api.Rest.Controllers.Spaces;

/// <summary>
/// Árbol de espacios de los sitios de la organización activa (D-06). Con alcance de sitio solo se ven los espacios de
/// los sitios del alcance, y con alcance de espacio, ese espacio y sus descendientes; lo demás responde como inexistente.
/// </summary>
[Tags("Spaces")]
[Route(Prefix)]
public sealed class SpacesController(ISpaceService spaces, ICurrentPermissions permissions) : ApiControllerBase
{
    private readonly SpacesScope scope = new(permissions);

    [HttpGet("sites/{siteId:guid}/spaces")]
    [RequirePermission(Permissions.SpacesRead)]
    [EndpointName("listSpaces")]
    [EndpointSummary("Listar los espacios de un sitio")]
    [EndpointDescription("En orden de árbol: cada espacio después de su padre y los hermanos por nombre. Con alcance de " +
        "espacio, solo ese espacio y sus descendientes.")]
    [ProducesResponseType<List<SpaceDto>>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> List(Guid siteId, CancellationToken ct)
    {
        if (!scope.Read.ReachesSite(siteId))
        {
            return NotFoundProblem();
        }

        return SpacesResults.ToHttp(await spaces.ListAsync(siteId, ct), list =>
            Results.Ok(scope.Read.CoversSite(siteId) ? list : list.Where(scope.CanRead).ToList()));
    }

    [HttpPost("sites/{siteId:guid}/spaces")]
    [RequirePermission(Permissions.SpacesManage)]
    [EndpointName("createSpace")]
    [EndpointSummary("Crear un espacio")]
    [EndpointDescription("Sin padre es la raíz del sitio y debe ser de tipo site; un sitio tiene una sola raíz. Con alcance de " +
        "espacio, el padre tiene que estar en el subárbol del alcance.")]
    [ProducesResponseType<SpaceDto>(StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create(Guid siteId, CreateSpaceRequest request, CancellationToken ct)
    {
        if (scope.DenyManageInSite(siteId) is { } denied)
        {
            return denied;
        }

        if (!scope.Manage.CoversSite(siteId))
        {
            // Solo alcance de espacio: la raíz exige el sitio entero y el padre tiene que estar en el subárbol.
            if (request.ParentId is not { } parentId)
            {
                return AccessProblems.PermissionDenied();
            }

            if ((await spaces.GetAsync(parentId, ct)).Value is not { } parent || parent.SiteId != siteId || !scope.CanManage(parent))
            {
                return SpacesResults.Validation(nameof(CreateSpaceRequest.ParentId), SpacesResults.MissingParentMessage);
            }
        }

        var input = new SpaceInput(request.Name!, request.Type!, request.ParentId, request.IsCommon ?? false);
        return SpacesResults.ToHttp(await spaces.CreateAsync(siteId, input, ct),
            space => Results.Created($"/api/v1/spaces/{space.Id}", space));
    }

    [HttpGet("spaces/{id:guid}")]
    [RequirePermission(Permissions.SpacesRead)]
    [EndpointName("getSpace")]
    [EndpointSummary("Obtener un espacio")]
    [EndpointDescription("Un espacio fuera del alcance responde igual que uno inexistente (404).")]
    [ProducesResponseType<SpaceDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is { } space ? Results.Ok(space) : NotFoundProblem();

    [HttpPut("spaces/{id:guid}")]
    [RequirePermission(Permissions.SpacesManage)]
    [EndpointName("updateSpace")]
    [EndpointSummary("Modificar un espacio")]
    [EndpointDescription("Cambia nombre, tipo y marca de espacio común; el sitio y el padre no cambian.")]
    [ProducesResponseType<SpaceDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Update(Guid id, UpdateSpaceRequest request, CancellationToken ct)
    {
        if (await VisibleAsync(id, ct) is not { } space)
        {
            return NotFoundProblem();
        }

        if (scope.DenyManage(space) is { } denied)
        {
            return denied;
        }

        var update = new SpaceUpdate(request.Name!, request.Type!, request.IsCommon ?? false);
        return SpacesResults.ToHttp(await spaces.UpdateAsync(id, update, ct), Results.Ok);
    }

    [HttpDelete("spaces/{id:guid}")]
    [RequirePermission(Permissions.SpacesManage)]
    [EndpointName("deleteSpace")]
    [EndpointSummary("Eliminar un espacio")]
    [EndpointDescription("Solo sin espacios hijos, puntos de acceso relacionados ni roles o invitaciones pendientes que lo " +
        "tengan como alcance.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Delete(Guid id, CancellationToken ct)
    {
        if (await VisibleAsync(id, ct) is not { } space)
        {
            return NotFoundProblem();
        }

        if (scope.DenyManage(space) is { } denied)
        {
            return denied;
        }

        return SpacesResults.ToHttp(await spaces.DeleteAsync(id, ct), _ => Results.NoContent());
    }

    [HttpGet("spaces/{id:guid}/access-path")]
    [RequirePermission(Permissions.SpacesRead)]
    [EndpointName("getAccessPath")]
    [EndpointSummary("Obtener el camino a un espacio")]
    [EndpointDescription("Puntos de acceso que dan acceso al espacio o a sus ancestros, desde la raíz y, a igual profundidad, " +
        "por nombre. Con alcance de espacio, el camino se devuelve completo.")]
    [ProducesResponseType<List<AccessPathEntryDto>>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> AccessPath(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is not null
            ? SpacesResults.ToHttp(await spaces.GetAccessPathAsync(id, ct), path => Results.Ok(path))
            : NotFoundProblem();

    /// <summary>El espacio, si existe en la organización activa y está en el alcance de lectura.</summary>
    private async Task<SpaceDto?> VisibleAsync(Guid id, CancellationToken ct) =>
        (await spaces.GetAsync(id, ct)).Value is { } space && scope.CanRead(space) ? space : null;
}
