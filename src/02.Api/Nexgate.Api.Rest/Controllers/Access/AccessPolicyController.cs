using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Access;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Access.Contracts;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Spaces.Contracts;

namespace Nexgate.Api.Rest.Controllers.Access;

/// <summary>
/// Política compilada de un punto de acceso y simulación de una pasada con la misma decisión que usa el firmware. La
/// simulación no registra eventos ni descuenta usos.
/// </summary>
[Tags("AccessPolicy")]
[Route(Prefix)]
public sealed class AccessPolicyController(
    IAccessPolicyService policies,
    ISpaceDirectory spaces,
    ICurrentPermissions permissions) : ApiControllerBase
{
    private readonly AccessScope scope = new(permissions);

    [HttpGet("access-points/{id:guid}/policy")]
    [RequirePermission(Permissions.AccessRead)]
    [EndpointName("getAccessPointPolicy")]
    [EndpointSummary("Obtener la política compilada de un punto de acceso")]
    [EndpointDescription("Formato v1: zona horaria, desplazamientos UTC, ventanas, credenciales (HMAC con la clave del sitio), " +
        "entradas y hash del contenido.")]
    [ProducesResponseType<CompiledPolicy>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Policy(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct)
            ? AccessResults.ToHttp(await policies.CompileAsync(id, ct), Results.Ok)
            : NotFoundProblem();

    [HttpPost("access-points/{id:guid}/simulations")]
    [RequirePermission(Permissions.AccessRead)]
    [EndpointName("simulateAccess")]
    [EndpointSummary("Simular una pasada por un punto de acceso")]
    [EndpointDescription("Compila la política vigente y decide con la credencial presentada en el instante indicado (ahora, si " +
        "no se indica). Motivos: allowed, credential_invalid, explicit_deny o no_permission.")]
    [ProducesResponseType<SimulationResultDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Simulate(Guid id, SimulationRequest request, CancellationToken ct) =>
        await VisibleAsync(id, ct)
            ? AccessResults.ToHttp(await policies.SimulateAsync(id, request.CredentialType!, request.CredentialValue!, request.At, ct), Results.Ok)
            : NotFoundProblem();

    private async Task<bool> VisibleAsync(Guid accessPointId, CancellationToken ct) =>
        await spaces.GetAccessPointAsync(accessPointId, ct) is { } point && scope.CanRead(point.SiteId);
}
