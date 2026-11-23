using System.ComponentModel.DataAnnotations;
using Nexgate.Service.Access.Contracts;
using Nexgate.Service.Devices.Contracts;

namespace Nexgate.Api.Rest.Models.Devices;

// Los atributos de validación van sobre los parámetros del constructor: los controllers de ASP.NET Core los leen de ahí en los records.

public sealed record DeviceRequest(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(DeviceLimits.NameMaxLength, ErrorMessage = "El nombre no puede superar 200 caracteres")]
    string? Name);

public sealed record AssignAccessPointsRequest(
    [Required(ErrorMessage = "Los puntos de acceso son obligatorios (vacío para ninguno)")]
    IReadOnlyList<Guid>? AccessPointIds);

/// <summary>Reclamo de un dispositivo: el código de la caja y una solicitud de certificado PKCS#10 (PEM) con clave P-256.</summary>
public sealed record ClaimRequest(
    [Required(ErrorMessage = "El código de reclamo es obligatorio")]
    [StringLength(40, ErrorMessage = "El código de reclamo no puede superar 40 caracteres")]
    string? ClaimCode,
    [Required(ErrorMessage = "La solicitud de certificado es obligatoria")]
    [StringLength(DeviceLimits.CertificateRequestMaxLength, ErrorMessage = "La solicitud de certificado no puede superar 4000 caracteres")]
    string? CertificateRequest);

public sealed record HeartbeatRequest(
    [StringLength(DeviceLimits.FirmwareVersionMaxLength, ErrorMessage = "La versión de firmware no puede superar 50 caracteres")]
    string? FirmwareVersion,
    [Range(0, int.MaxValue, ErrorMessage = "La versión de política no puede ser negativa")]
    int? PolicyVersion);

/// <summary>
/// Paquete de política de un dispositivo, formato 1 (documentación del cuerpo de <c>GET /device/policy</c>). La firma
/// ECDSA P-256 de los bytes exactos del cuerpo viaja en el encabezado <c>X-Nexgate-Signature</c>.
/// </summary>
public sealed record DevicePolicyResponse(
    int Format,
    Guid DeviceId,
    Guid SiteId,
    int Version,
    DateTimeOffset IssuedAt,
    IReadOnlyList<CompiledPolicy> Points);

/// <summary>
/// Un evento de acceso tal como lo decidió el dispositivo. <c>Credential</c> es el hash leído (base64url, como en la
/// política), si lo hubo; nunca se guarda: se resuelve a la credencial y su sujeto.
/// </summary>
public sealed record DeviceEventRequest(
    Guid? Id,
    DateTimeOffset? OccurredAt,
    Guid? AccessPointId,
    string? Credential,
    // Decisión (allow, deny) y motivo los valida el servicio junto con el resto del lote, así el 400 nombra todos los errores.
    string? Decision,
    string? Reason,
    int? PolicyVersion);

/// <summary>Lote de eventos del dispositivo (1 a 100), incluidos los que encoló sin conexión.</summary>
public sealed record DeviceEventsRequest(
    [Required(ErrorMessage = "Los eventos son obligatorios")]
    IReadOnlyList<DeviceEventRequest>? Events);

