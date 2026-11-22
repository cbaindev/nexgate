using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Access;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Access.Contracts;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Controllers.Access;

/// <summary>Credenciales de los sujetos. Ninguna respuesta incluye el valor ni su hash.</summary>
[Tags("Credentials")]
[Route(Prefix)]
public sealed class CredentialsController(ICredentialService credentials, ICurrentPermissions permissions) : ApiControllerBase
{
    private readonly AccessScope scope = new(permissions);

    [HttpGet("sites/{siteId:guid}/credentials")]
    [RequirePermission(Permissions.AccessRead)]
    [EndpointName("listCredentials")]
    [EndpointSummary("Listar las credenciales de un sitio")]
    [EndpointDescription("Opcionalmente, solo las de un sujeto. Sin el valor ni su hash.")]
    [ProducesResponseType<List<CredentialDto>>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> List(Guid siteId, [FromQuery] Guid? subjectId, CancellationToken ct) =>
        scope.CanRead(siteId)
            ? AccessResults.ToHttp(await credentials.ListAsync(siteId, subjectId, ct), list => Results.Ok(list))
            : NotFoundProblem();

    [HttpPost("sites/{siteId:guid}/credentials")]
    [RequirePermission(Permissions.AccessManage)]
    [EndpointName("createCredential")]
    [EndpointSummary("Registrar una credencial")]
    [EndpointDescription("Control RF, tarjeta o PIN de un sujeto del sitio. Se guarda solo el HMAC del valor con la clave del " +
        "sitio; el valor no se devuelve nunca.")]
    [ProducesResponseType<CredentialDto>(StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create(Guid siteId, CreateCredentialRequest request, CancellationToken ct) =>
        scope.DenyManage(siteId) ?? AccessResults.ToHttp(
            await credentials.CreateAsync(siteId,
                new CredentialInput(request.SubjectId!.Value, request.Type!, request.Value!, request.ValidFrom, request.ValidUntil), ct),
            credential => Results.Created($"/api/v1/credentials/{credential.Id}", credential));

    [HttpGet("credentials/{id:guid}")]
    [RequirePermission(Permissions.AccessRead)]
    [EndpointName("getCredential")]
    [EndpointSummary("Obtener una credencial")]
    [ProducesResponseType<CredentialDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is { } credential ? Results.Ok(credential) : NotFoundProblem();

    [HttpPut("credentials/{id:guid}")]
    [RequirePermission(Permissions.AccessManage)]
    [EndpointName("updateCredential")]
    [EndpointSummary("Modificar la vigencia de una credencial")]
    [ProducesResponseType<CredentialDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(Guid id, UpdateCredentialRequest request, CancellationToken ct) =>
        await VisibleAsync(id, ct) is not { } credential
            ? NotFoundProblem()
            : scope.DenyManage(credential.SiteId) ?? AccessResults.ToHttp(
                await credentials.UpdateAsync(id, new CredentialUpdate(request.ValidFrom, request.ValidUntil), ct), Results.Ok);

    [HttpDelete("credentials/{id:guid}")]
    [RequirePermission(Permissions.AccessManage)]
    [EndpointName("deleteCredential")]
    [EndpointSummary("Eliminar una credencial")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is not { } credential
            ? NotFoundProblem()
            : scope.DenyManage(credential.SiteId) ?? AccessResults.ToHttp(await credentials.DeleteAsync(id, ct), _ => Results.NoContent());

    private async Task<CredentialDto?> VisibleAsync(Guid id, CancellationToken ct) =>
        (await credentials.GetAsync(id, ct)).Value is { } credential && scope.CanRead(credential.SiteId) ? credential : null;
}
