using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Extensions;
using Nexgate.Api.Rest.Models.Identity;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Controllers.Identity;

[Tags("Invitations")]
[Route(Prefix + "invitations")]
public sealed class InvitationsController(IInvitationService invitations, RequestContext context) : ApiControllerBase
{
    [HttpPost]
    [RequirePermission(Permissions.MembersManage, AtOrganization = true)]
    [EndpointName("createInvitation")]
    [EndpointSummary("Invitar a una persona a la organización activa")]
    [EndpointDescription("Crea una invitación con rol y alcance. La respuesta es la misma tenga o no cuenta la persona invitada. " +
        "Mientras no haya envío de correos, el token de la invitación se devuelve a quien invita.")]
    [ProducesResponseType<IssuedInvitation>(StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create(InviteRequest request, CancellationToken ct) =>
        IdentityResults.ToHttp(await invitations.InviteAsync(context.OrganizationId!.Value, context.UserId!.Value,
                new InvitationInput(request.Email, request.Role, request.ScopeType, request.SiteId, request.SpaceId), ct),
            issued => Results.Created($"/api/v1/invitations/{issued.Id}", issued));

    [HttpPost("accept")]
    [AnonymousAccess(AcceptsAccessToken = true)]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [EndpointName("acceptInvitation")]
    [EndpointSummary("Aceptar una invitación")]
    [EndpointDescription("Sin cuenta, la crea con la contraseña indicada. Con cuenta existente, exige el token de acceso de esa " +
        "cuenta (401 si no viene). Después hay que iniciar sesión para obtener tokens con la organización.")]
    [ProducesResponseType<AcceptInvitationResponse>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    [ProblemResponse(StatusCodes.Status401Unauthorized)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Accept(AcceptInvitationRequest request, CancellationToken ct) =>
        IdentityResults.ToHttp(await invitations.AcceptAsync(request.Token, request.Password, context.UserId, ct),
            accepted => Results.Ok(new AcceptInvitationResponse(accepted.UserId, accepted.OrganizationId)));
}
