using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Identity;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Controllers.Identity;

[Tags("Me")]
[Route(Prefix + "me")]
[RequireAuthenticatedUser]
public sealed class MeController(ICurrentUserService currentUser, RequestContext context) : ApiControllerBase
{
    [HttpGet]
    [EndpointName("getCurrentUser")]
    [EndpointSummary("Usuario actual y sus organizaciones")]
    [EndpointDescription("Devuelve la cuenta, la organización activa y las organizaciones donde el usuario es miembro, para el selector.")]
    [ProducesResponseType<CurrentUserInfo>(StatusCodes.Status200OK)]
    public async Task<IResult> Get(CancellationToken ct) =>
        await currentUser.GetAsync(context.UserId!.Value, context.OrganizationId, ct) is { } me
            ? Results.Ok(me)
            : AccessProblems.AuthenticationRequired();

    [HttpGet("permissions")]
    [EndpointName("getMyPermissions")]
    [EndpointSummary("Permisos efectivos en la organización activa")]
    [EndpointDescription("Permisos administrativos del usuario según sus roles vigentes, con alcance de organización o la lista " +
        "de sitios. La interfaz muestra solo lo permitido; el servidor igual los verifica en cada operación.")]
    [ProducesResponseType<PermissionsResponse>(StatusCodes.Status200OK)]
    public IResult GetPermissions() => Results.Ok(new PermissionsResponse(
        context.OrganizationId,
        context.Grants
            .GroupBy(g => g.Permission)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new PermissionResponse(
                g.Key,
                g.Any(x => x.SiteId is null),
                g.Any(x => x.SiteId is null) ? [] : g.Select(x => x.SiteId!.Value).Distinct().Order().ToList()))
            .ToList()));
}
