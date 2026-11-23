using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Extensions;
using Nexgate.Api.Rest.Models.Devices;
using Nexgate.Api.Rest.Controllers.Events;
using Nexgate.Service.Devices.Contracts;
using Nexgate.Service.Events.Contracts;

namespace Nexgate.Api.Rest.Controllers.Devices;

/// <summary>
/// Protocolo de dispositivos por HTTPS (design de add-device-sync-protocol, decisión 1): reclamo con el código de la caja,
/// y con el certificado de cliente, política firmada, latido y eventos.
/// </summary>
[Tags("DeviceProtocol")]
[Route(Prefix + "device")]
public sealed class DeviceProtocolController(
    IDeviceClaimService claims,
    IDevicePolicyService policies,
    IAccessEventIngestion ingestion) : ApiControllerBase
{
    public const string SignatureHeader = "X-Nexgate-Signature";

    [HttpPost("claim")]
    [AnonymousAccess]
    [EnableRateLimiting(RateLimitingExtensions.DeviceClaimPolicy)]
    [EndpointName("claimDevice")]
    [EndpointSummary("Reclamar un dispositivo")]
    [EndpointDescription("Con el código de reclamo y una solicitud de certificado PKCS#10 (PEM, clave P-256). Devuelve el " +
        "certificado de cliente, el de la autoridad, la clave HMAC del sitio (base64) y la clave pública que verifica las " +
        "políticas. Un código inexistente, usado o vencido responde 400 invalid_claim.")]
    [ProducesResponseType<DeviceClaim>(StatusCodes.Status200OK)]
    public async Task<IResult> Claim(ClaimRequest request, CancellationToken ct) =>
        DevicesResults.ToHttp(await claims.ClaimAsync(request.ClaimCode, request.CertificateRequest, ct), Results.Ok);

    [HttpGet("policy")]
    [RequireDevice]
    [EndpointName("getDevicePolicy")]
    [EndpointSummary("Descargar la política del dispositivo")]
    [EndpointDescription("Paquete con la política v1 de cada punto que controla el dispositivo y una versión que solo avanza " +
        "cuando cambia el contenido (encabezado ETag). Con If-None-Match y la versión vigente responde 304. La firma ECDSA " +
        "P-256 (IEEE P1363, base64url) de los bytes exactos del cuerpo viaja en X-Nexgate-Signature.")]
    [ProducesResponseType<DevicePolicyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IResult> Policy(CancellationToken ct)
    {
        var result = await policies.GetPolicyAsync(HttpContext.Device(), ct);
        if (result.Value is not { } package)
        {
            return DevicesResults.ToHttp(result, _ => Results.Empty);
        }

        var etag = new EntityTagHeaderValue($"\"{package.Version}\"");
        Response.Headers.ETag = etag.ToString();
        if (Request.GetTypedHeaders().IfNoneMatch.Any(tag => tag.Compare(etag, useStrongComparison: false)))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers[SignatureHeader] = package.Signature;
        return Results.Bytes(package.Content, "application/json");
    }

    [HttpPost("heartbeat")]
    [RequireDevice]
    [EndpointName("deviceHeartbeat")]
    [EndpointSummary("Latido del dispositivo")]
    [EndpointDescription("Registra el último contacto y las versiones de firmware y de política. Responde la versión de " +
        "política vigente, la hora del servidor (UTC) y cada cuántos segundos volver a latir.")]
    [ProducesResponseType<HeartbeatResult>(StatusCodes.Status200OK)]
    public async Task<IResult> Heartbeat(HeartbeatRequest request, CancellationToken ct) =>
        DevicesResults.ToHttp(
            await policies.HeartbeatAsync(HttpContext.Device(), new HeartbeatInput(request.FirmwareVersion, request.PolicyVersion), ct),
            Results.Ok);

    [HttpPost("events")]
    [RequireDevice]
    [EndpointName("sendDeviceEvents")]
    [EndpointSummary("Enviar eventos de acceso")]
    [EndpointDescription("Lote de 1 a 100 eventos con el identificador que generó el dispositivo (un reenvío no duplica), el " +
        "instante en que ocurrieron (también los encolados sin conexión), el punto, el hash de la credencial si la leyó, la " +
        "decisión, el motivo y la versión de política. Se valida el lote entero antes de guardar.")]
    [ProducesResponseType<IngestResult>(StatusCodes.Status200OK)]
    public async Task<IResult> Events(DeviceEventsRequest request, CancellationToken ct)
    {
        var device = HttpContext.Device();
        var events = request.Events!
            .Select(e => e is null ? null! : new AccessEventInput(e.Id, e.OccurredAt, e.AccessPointId, e.Credential, e.Decision, e.Reason, e.PolicyVersion))
            .ToList();
        return EventsResults.ToHttp(await ingestion.IngestAsync(device.DeviceId, device.SiteId, events, ct), Results.Ok);
    }
}
