using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Tenancy;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Common.BuildingBlocks.Tenancy;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Api.Rest.Controllers.Tenancy;

/// <summary>
/// Sitios de la organización activa. Con alcance de sitio solo se ven los sitios del alcance, y con alcance de espacio, el
/// sitio que lo contiene (solo lectura); un sitio fuera del alcance responde como inexistente.
/// </summary>
[Tags("Sites")]
[Route(Prefix + "sites")]
public sealed class SitesController(ISiteService sites, ICurrentPermissions permissions, ICurrentOrganization organization)
    : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.SitesRead)]
    [EndpointName("listSites")]
    [EndpointSummary("Listar los sitios")]
    [EndpointDescription("Con alcance de sitio, solo los sitios del alcance.")]
    [ProducesResponseType<List<SiteDto>>(StatusCodes.Status200OK)]
    public async Task<IResult> List(CancellationToken ct) =>
        Results.Ok(await sites.ListAsync(ReachableSites(permissions.ScopeFor(Permissions.SitesRead)), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.SitesRead)]
    [EndpointName("getSite")]
    [EndpointSummary("Obtener un sitio")]
    [EndpointDescription("Un sitio fuera del alcance responde igual que uno inexistente (404).")]
    [ProducesResponseType<SiteDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid id, CancellationToken ct) =>
        permissions.ScopeFor(Permissions.SitesRead).ReachesSite(id)
            ? TenancyResults.ToHttp(await sites.GetAsync(id, ct), Results.Ok)
            : NotFoundProblem();

    [HttpPost]
    [RequirePermission(Permissions.SitesManage, AtOrganization = true)]
    [EndpointName("createSite")]
    [EndpointSummary("Crear un sitio")]
    [EndpointDescription("Exige sites.manage con alcance de organización.")]
    [ProducesResponseType<SiteDto>(StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create(CreateSiteRequest request, CancellationToken ct)
    {
        var input = new SiteInput(request.Name!, request.Type!, request.TimeZone!, ConfigurationText(request.Configuration));
        return TenancyResults.ToHttp(await sites.CreateAsync(organization.Id!.Value, input, ct),
            site => Results.Created($"/api/v1/sites/{site.Id}", site));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.SitesManage)]
    [EndpointName("updateSite")]
    [EndpointSummary("Modificar un sitio")]
    [EndpointDescription("Un sitio visible pero fuera del alcance de sites.manage responde 403; uno no visible, 404.")]
    [ProducesResponseType<SiteDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Update(Guid id, UpdateSiteRequest request, CancellationToken ct)
    {
        if (!permissions.SitesFor(Permissions.SitesManage).Allows(id))
        {
            // Puede verlo pero no modificarlo: 403. Ni siquiera puede verlo: igual que inexistente.
            return permissions.ScopeFor(Permissions.SitesRead).ReachesSite(id) ? AccessProblems.PermissionDenied() : NotFoundProblem();
        }

        var update = new SiteUpdate(request.Name!, request.TimeZone!, ConfigurationText(request.Configuration));
        return TenancyResults.ToHttp(await sites.UpdateAsync(id, update, ct), Results.Ok);
    }

    /// <summary>Sitios que alcanza el permiso: enteros o por alguno de sus espacios; null es toda la organización.</summary>
    private static IReadOnlyCollection<Guid>? ReachableSites(PermissionScope scope) =>
        scope.Organization ? null : scope.Sites.Concat(scope.Spaces.Select(s => s.SiteId)).ToHashSet();

    private static string? ConfigurationText(JsonElement? configuration) =>
        configuration is { ValueKind: JsonValueKind.Object } value ? value.GetRawText() : null;
}
