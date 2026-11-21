using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Identity;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Controllers.Identity;

/// <summary>Reemisión de la invitación del primer administrador (solo para el operador de plataforma).</summary>
[Tags("Platform")]
[Route(Prefix + "platform/organizations/{organizationId:guid}/administrator-invitations")]
public sealed class PlatformAdministratorInvitationsController(IAdministratorInvitations invitations, ICurrentUser user)
    : ApiControllerBase
{
    [HttpPost]
    [RequirePlatformOperator]
    [EndpointName("reissueAdministratorInvitation")]
    [EndpointSummary("Emitir una nueva invitación de administrador")]
    [EndpointDescription("Para cuando la invitación del primer administrador se perdió o venció. Solo para el operador de plataforma.")]
    [ProducesResponseType<IssuedInvitation>(StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Create(Guid organizationId, AdministratorInvitationRequest request, CancellationToken ct) =>
        await invitations.CreateAsync(organizationId, request.Email!, user.Id!.Value, ct) switch
        {
            { Status: AdministratorInvitationStatus.Created, Invitation: { } issued } =>
                Results.Created($"/api/v1/invitations/{issued.Id}", issued),
            { Status: AdministratorInvitationStatus.InvalidEmail } => IdentityResults.Validation("Email", "Correo electrónico inválido."),
            _ => IdentityResults.NotFound()
        };
}
