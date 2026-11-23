namespace Nexgate.Service.Devices.Contracts;

/// <summary>
/// Un dispositivo de un sitio (design de add-device-sync-protocol, decisión 5). Nunca lleva el código de reclamo, el
/// certificado ni su huella.
/// </summary>
public sealed record DeviceDto(
    Guid Id,
    Guid SiteId,
    string Name,
    string Status,
    IReadOnlyList<Guid> AccessPointIds,
    DateTimeOffset? LastSeenAt,
    string? FirmwareVersion,
    int? ReportedPolicyVersion,
    int? PolicyVersion,
    DateTimeOffset? CertificateExpiresAt,
    DateTimeOffset? ClaimExpiresAt);

/// <summary>Un dispositivo con su código de reclamo, que solo existe en la respuesta que lo emite.</summary>
public sealed record IssuedDevice(DeviceDto Device, string ClaimCode, DateTimeOffset ClaimExpiresAt);

/// <summary>Valores públicos del estado de un dispositivo.</summary>
public static class DeviceStatusNames
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Revoked = "revoked";
}

public static class DeviceLimits
{
    public const int NameMaxLength = 200;
    public const int FirmwareVersionMaxLength = 50;
    public const int CertificateRequestMaxLength = 4000;
    public const int MaxEventsPerBatch = 100;
}

/// <summary>Códigos de error estables del módulo.</summary>
public static class DevicesErrorCodes
{
    public const string DeviceNameConflict = "device_name_conflict";
    public const string AccessPointAssigned = "access_point_assigned";
    public const string InvalidClaim = "invalid_claim";
    public const string DeviceAuthenticationRequired = "device_authentication_required";
}

public enum DevicesErrorKind
{
    Validation,
    NotFound,
    Conflict
}

public sealed record DevicesError(DevicesErrorKind Kind, string? Field = null, string? Message = null, string? Code = null);

/// <summary>Resultado de un caso de uso: un valor o un error esperado.</summary>
public sealed record DevicesResult<T>(T? Value, DevicesError? Error)
{
    public static DevicesResult<T> Ok(T value) => new(value, null);

    public static DevicesResult<T> Fail(DevicesError error) => new(default, error);

    public static DevicesResult<T> NotFound() => new(default, new DevicesError(DevicesErrorKind.NotFound));

    public static DevicesResult<T> Invalid(string field, string message, string? code = null) =>
        new(default, new DevicesError(DevicesErrorKind.Validation, field, message, code));
}

/// <summary>Dispositivos de los sitios de la organización activa (panel).</summary>
public interface IDeviceService
{
    /// <summary>Dispositivos del sitio ordenados por nombre; NotFound si el sitio no existe en la organización activa.</summary>
    Task<DevicesResult<IReadOnlyList<DeviceDto>>> ListAsync(Guid siteId, CancellationToken cancellationToken);

    Task<DevicesResult<DeviceDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<DevicesResult<IssuedDevice>> CreateAsync(Guid siteId, string name, Guid createdBy, CancellationToken cancellationToken);

    Task<DevicesResult<DeviceDto>> RenameAsync(Guid id, string name, CancellationToken cancellationToken);

    /// <summary>Reemplaza los puntos que controla el dispositivo (del mismo sitio, ninguno de otro dispositivo).</summary>
    Task<DevicesResult<DeviceDto>> AssignAsync(Guid id, IReadOnlyList<Guid> accessPointIds, CancellationToken cancellationToken);

    /// <summary>Nuevo código de reclamo: el dispositivo vuelve a pendiente y su certificado deja de valer.</summary>
    Task<DevicesResult<IssuedDevice>> IssueClaimCodeAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>El certificado deja de valer; el dispositivo conserva sus puntos y se puede volver a reclamar.</summary>
    Task<DevicesResult<DeviceDto>> RevokeAsync(Guid id, CancellationToken cancellationToken);

    Task<DevicesResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Identidad de un dispositivo autenticado por su certificado: su organización y su sitio.</summary>
public sealed record DeviceIdentity(Guid DeviceId, Guid OrganizationId, Guid SiteId);

/// <summary>
/// Respuesta del reclamo. Es la única que lleva la clave HMAC del sitio (base64); nada de esto se registra en logs.
/// </summary>
public sealed record DeviceClaim(
    Guid DeviceId,
    Guid SiteId,
    string Certificate,
    string AuthorityCertificate,
    string CredentialKey,
    string PolicyKey,
    int PollSeconds);

/// <summary>Reclamo de un dispositivo con su código (sin otra autenticación).</summary>
public interface IDeviceClaimService
{
    Task<DevicesResult<DeviceClaim>> ClaimAsync(string? claimCode, string? certificateRequest, CancellationToken cancellationToken);
}

/// <summary>
/// Reconoce el certificado vigente de un dispositivo activo (por su huella SHA-256) y registra el último contacto. No
/// necesita organización activa: la devuelve. La cadena ya la verificó la autenticación.
/// </summary>
public interface IDeviceAuthenticator
{
    /// <summary>El certificado de cliente presentado, en DER.</summary>
    Task<DeviceIdentity?> AuthenticateAsync(byte[] certificate, CancellationToken cancellationToken);
}

/// <summary>
/// Paquete de política de un dispositivo: el JSON exacto (<see cref="Content"/>) y su firma ECDSA P-256 sobre esos bytes
/// (IEEE P1363, base64url).
/// </summary>
public sealed record DevicePolicyPackage(int Version, byte[] Content, string Signature);

public sealed record HeartbeatInput(string? FirmwareVersion, int? PolicyVersion);

public sealed record HeartbeatResult(int PolicyVersion, DateTimeOffset ServerTime, int PollSeconds);

/// <summary>Política y latido del dispositivo de la solicitud (con su organización activa).</summary>
public interface IDevicePolicyService
{
    Task<DevicesResult<DevicePolicyPackage>> GetPolicyAsync(DeviceIdentity device, CancellationToken cancellationToken);

    Task<DevicesResult<HeartbeatResult>> HeartbeatAsync(DeviceIdentity device, HeartbeatInput input, CancellationToken cancellationToken);
}
