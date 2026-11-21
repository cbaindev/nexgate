using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Identity;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Controllers.Identity;

[Tags("Members")]
[Route(Prefix + "members")]
public sealed class MembersController(IMemberService members, RequestContext context) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.MembersRead, AtOrganization = true)]
    [EndpointName("listMembers")]
    [EndpointSummary("Listar los miembros de la organización activa")]
    [ProducesResponseType<List<MemberResponse>>(StatusCodes.Status200OK)]
    public async Task<IResult> List(CancellationToken ct) =>
        Results.Ok((await members.ListAsync(ct))
            .Select(m => new MemberResponse(m.UserId, m.Email, m.RoleAssignments.Select(RoleAssignmentResponse.From).ToList()))
            .ToList());

    [HttpDelete("{userId:guid}")]
    [RequirePermission(Permissions.MembersManage, AtOrganization = true)]
    [EndpointName("removeMember")]
    [EndpointSummary("Dar de baja a un miembro")]
    [EndpointDescription("Termina la membresía y sus roles con efecto en la solicitud siguiente. No se puede dar de baja al " +
        "último administrador (409 last_administrator).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Remove(Guid userId, CancellationToken ct) =>
        IdentityResults.ToHttp(await members.RemoveMemberAsync(userId, ct));

    [HttpPost("{userId:guid}/role-assignments")]
    [RequirePermission(Permissions.MembersManage, AtOrganization = true)]
    [EndpointName("assignRole")]
    [EndpointSummary("Asignar un rol a un miembro")]
    [EndpointDescription("Rol con alcance de organización o de un sitio, con vigencia opcional.")]
    [ProducesResponseType<RoleAssignmentResponse>(StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> AssignRole(Guid userId, AssignRoleRequest request, CancellationToken ct) =>
        IdentityResults.ToHttp(await members.AssignRoleAsync(context.OrganizationId!.Value, userId,
                new RoleAssignmentInput(request.Role, request.ScopeType, request.SiteId, request.ValidFrom, request.ValidUntil), ct),
            assignment => Results.Created($"/api/v1/members/{userId}/role-assignments/{assignment.Id}",
                RoleAssignmentResponse.From(assignment)));

    [HttpDelete("{userId:guid}/role-assignments/{assignmentId:guid}")]
    [RequirePermission(Permissions.MembersManage, AtOrganization = true)]
    [EndpointName("revokeRole")]
    [EndpointSummary("Revocar un rol de un miembro")]
    [EndpointDescription("No se puede revocar el último rol de administrador vigente de la organización (409 last_administrator).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> RevokeRole(Guid userId, Guid assignmentId, CancellationToken ct) =>
        IdentityResults.ToHttp(await members.RevokeRoleAsync(userId, assignmentId, ct));
}
