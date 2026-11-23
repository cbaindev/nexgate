using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Devices;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Devices.Contracts;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Controllers.Devices;

/// <summary>
/// Dispositivos de los sitios de la organización activa: alta con código de reclamo, puntos que controlan, revocación y
/// eliminación. El código de reclamo solo aparece en la respuesta que lo emite.
/// </summary>
[Tags("Devices")]
[Route(Prefix)]
public sealed class DevicesController(IDeviceService devices, ICurrentPermissions permissions, RequestContext context) : ApiControllerBase
{
    private readonly SiteScope scope = SiteScope.Devices(permissions);

    [HttpGet("sites/{siteId:guid}/devices")]
    [RequirePermission(Permissions.DevicesRead)]
    [EndpointName("listDevices")]
    [EndpointSummary("Listar los dispositivos de un sitio")]
    [EndpointDescription("Ordenados por nombre, con su estado, sus puntos de acceso, el último contacto y las versiones.")]
    [ProducesResponseType<List<DeviceDto>>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> List(Guid siteId, CancellationToken ct) =>
        scope.CanRead(siteId) ? DevicesResults.ToHttp(await devices.ListAsync(siteId, ct), list => Results.Ok(list)) : NotFoundProblem();

    [HttpPost("sites/{siteId:guid}/devices")]
    [RequirePermission(Permissions.DevicesManage)]
    [EndpointName("createDevice")]
    [EndpointSummary("Registrar un dispositivo")]
    [EndpointDescription("Queda pendiente con un código de reclamo de un solo uso que vence a los 7 días. El código solo " +
        "aparece en esta respuesta.")]
    [ProducesResponseType<IssuedDevice>(StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create(Guid siteId, DeviceRequest request, CancellationToken ct) =>
        scope.DenyManage(siteId) ?? DevicesResults.ToHttp(
            await devices.CreateAsync(siteId, request.Name!, context.UserId!.Value, ct),
            issued => Results.Created($"/api/v1/devices/{issued.Device.Id}", issued));

    [HttpGet("devices/{id:guid}")]
    [RequirePermission(Permissions.DevicesRead)]
    [EndpointName("getDevice")]
    [EndpointSummary("Obtener un dispositivo")]
    [ProducesResponseType<DeviceDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is { } device ? Results.Ok(device) : NotFoundProblem();

    [HttpPut("devices/{id:guid}")]
    [RequirePermission(Permissions.DevicesManage)]
    [EndpointName("updateDevice")]
    [EndpointSummary("Cambiar el nombre de un dispositivo")]
    [ProducesResponseType<DeviceDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Update(Guid id, DeviceRequest request, CancellationToken ct) =>
        await VisibleAsync(id, ct) is not { } device
            ? NotFoundProblem()
            : scope.DenyManage(device.SiteId) ?? DevicesResults.ToHttp(await devices.RenameAsync(id, request.Name!, ct), Results.Ok);

    [HttpPut("devices/{id:guid}/access-points")]
    [RequirePermission(Permissions.DevicesManage)]
    [EndpointName("assignDeviceAccessPoints")]
    [EndpointSummary("Asignar los puntos de acceso que controla un dispositivo")]
    [EndpointDescription("Reemplaza el conjunto: puntos del sitio del dispositivo que no controle otro dispositivo " +
        "(409 access_point_assigned).")]
    [ProducesResponseType<DeviceDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Assign(Guid id, AssignAccessPointsRequest request, CancellationToken ct) =>
        await VisibleAsync(id, ct) is not { } device
            ? NotFoundProblem()
            : scope.DenyManage(device.SiteId) ?? DevicesResults.ToHttp(await devices.AssignAsync(id, request.AccessPointIds!, ct), Results.Ok);

    [HttpPost("devices/{id:guid}/claim-code")]
    [RequirePermission(Permissions.DevicesManage)]
    [EndpointName("issueDeviceClaimCode")]
    [EndpointSummary("Emitir un nuevo código de reclamo")]
    [EndpointDescription("El dispositivo vuelve a pendiente y su certificado anterior deja de valer. El código solo aparece " +
        "en esta respuesta.")]
    [ProducesResponseType<IssuedDevice>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> IssueClaimCode(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is not { } device
            ? NotFoundProblem()
            : scope.DenyManage(device.SiteId) ?? DevicesResults.ToHttp(await devices.IssueClaimCodeAsync(id, ct), Results.Ok);

    [HttpPost("devices/{id:guid}/revocation")]
    [RequirePermission(Permissions.DevicesManage)]
    [EndpointName("revokeDevice")]
    [EndpointSummary("Revocar un dispositivo")]
    [EndpointDescription("Su certificado deja de valer en la solicitud siguiente. Conserva sus puntos y se puede volver a " +
        "reclamar con un código nuevo.")]
    [ProducesResponseType<DeviceDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Revoke(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is not { } device
            ? NotFoundProblem()
            : scope.DenyManage(device.SiteId) ?? DevicesResults.ToHttp(await devices.RevokeAsync(id, ct), Results.Ok);

    [HttpDelete("devices/{id:guid}")]
    [RequirePermission(Permissions.DevicesManage)]
    [EndpointName("deleteDevice")]
    [EndpointSummary("Eliminar un dispositivo")]
    [EndpointDescription("Su certificado deja de valer y sus puntos quedan libres. Sus eventos se conservan.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is not { } device
            ? NotFoundProblem()
            : scope.DenyManage(device.SiteId) ?? DevicesResults.ToHttp(await devices.DeleteAsync(id, ct), _ => Results.NoContent());

    private async Task<DeviceDto?> VisibleAsync(Guid id, CancellationToken ct) =>
        (await devices.GetAsync(id, ct)).Value is { } device && scope.CanRead(device.SiteId) ? device : null;
}
