using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Tenancy;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Api.Rest.Controllers.Tenancy;

/// <summary>Operaciones de plataforma sobre organizaciones (solo para el operador de plataforma).</summary>
[Tags("Platform")]
[Route(Prefix + "platform/organizations")]
public sealed class PlatformOrganizationsController(
    IOrganizationService organizations,
    IAdministratorInvitations invitations,
    ICurrentUser user) : ApiControllerBase
{
    /// <remarks>
    /// Crea la organización y la invitación de su primer administrador. Son dos escrituras en dos módulos; si la segunda
    /// falla, el operador reemite la invitación (design de add-identity-and-roles, 11).
    /// </remarks>
    [HttpPost]
    [RequirePlatformOperator]
    [EndpointName("createOrganization")]
    [EndpointSummary("Crear una organización")]
    [EndpointDescription("Crea la organización y la invitación de su primer administrador, que se devuelve en " +
        "administratorInvitation. Solo para el operador de plataforma.")]
    [ProducesResponseType<CreatedOrganizationResponse>(StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create(CreateOrganizationRequest request, CancellationToken ct)
    {
        var created = await organizations.CreateAsync(request.Name!, ct);
        if (created is not { Error: null, Value: { } organization })
        {
            return TenancyResults.ToHttp(created, Results.Ok);
        }

        var invitation = await invitations.CreateAsync(organization.Id, request.AdministratorEmail!, user.Id!.Value, ct);
        return Results.Created("/api/v1/organization",
            new CreatedOrganizationResponse(organization.Id, organization.Name, organization.Status, invitation.Invitation));
    }
}
