using Nexgate.Service.Devices.Contracts;

namespace Nexgate.Domain.Devices;

/// <summary>Una regla del dominio no se cumple. El campo usa el nombre de la propiedad pública de la solicitud.</summary>
public sealed class DomainValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

public enum DeviceStatus
{
    Pending,
    Active,
    Revoked
}

public static class DeviceStatuses
{
    public static string ToName(DeviceStatus status) => status switch
    {
        DeviceStatus.Pending => DeviceStatusNames.Pending,
        DeviceStatus.Active => DeviceStatusNames.Active,
        DeviceStatus.Revoked => DeviceStatusNames.Revoked,
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static DeviceStatus Parse(string value) => value switch
    {
        DeviceStatusNames.Pending => DeviceStatus.Pending,
        DeviceStatusNames.Active => DeviceStatus.Active,
        DeviceStatusNames.Revoked => DeviceStatus.Revoked,
        _ => throw new InvalidOperationException("Estado de dispositivo desconocido.")
    };
}

/// <summary>Un punto de acceso que controla un dispositivo. Un punto lo controla a lo sumo un dispositivo.</summary>
public sealed class DeviceAccessPoint
{
    private DeviceAccessPoint()
    {
    }

    public DeviceAccessPoint(Guid deviceId, Guid accessPointId)
    {
        DeviceId = deviceId;
        AccessPointId = accessPointId;
    }

    public Guid DeviceId { get; private set; }

    public Guid AccessPointId { get; private set; }

    public Guid OrganizationId { get; private set; }
}

/// <summary>
/// Controlador de puerta de un sitio (docs/04). Nace pendiente con un código de reclamo; al reclamarlo (función de base
/// de datos) queda activo con la huella de su certificado. Revocarlo o emitir otro código invalida ese certificado.
/// </summary>
public sealed class Device
{
    private readonly List<DeviceAccessPoint> _accessPoints = [];

    private Device()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Organización dueña. La asigna la persistencia con la organización activa y no cambia nunca.</summary>
    public Guid OrganizationId { get; private set; }

    public Guid SiteId { get; private set; }

    public string Name { get; private set; }

    public DeviceStatus Status { get; private set; }

    /// <summary>SHA-256 del código de reclamo normalizado; nulo cuando no hay código vigente.</summary>
    public byte[]? ClaimCodeHash { get; private set; }

    public DateTimeOffset? ClaimExpiresAt { get; private set; }

    /// <summary>SHA-256 (hex en minúsculas) del certificado vigente.</summary>
    public string? CertificateThumbprint { get; private set; }

    public DateTimeOffset? CertificateExpiresAt { get; private set; }

    public string? FirmwareVersion { get; private set; }

    public int? ReportedPolicyVersion { get; private set; }

    public DateTimeOffset? LastSeenAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<DeviceAccessPoint> AccessPoints => _accessPoints;

    /// <summary>Crea el dispositivo pendiente y devuelve su código de reclamo (solo existe en esta respuesta).</summary>
    public static (Device Device, string ClaimCode) Create(Guid siteId, string name, Guid createdBy, DateTimeOffset now, TimeSpan claimValidity)
    {
        if (siteId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(SiteId), "El dispositivo debe pertenecer a un sitio.");
        }

        var device = new Device { Id = Guid.CreateVersion7(now), SiteId = siteId, CreatedBy = createdBy, CreatedAt = now };
        device.Rename(name, now);
        var code = device.IssueClaimCode(now, claimValidity);
        return (device, code);
    }

    public static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Name), "El nombre es obligatorio.");
        }

        if (trimmed.Length > DeviceLimits.NameMaxLength)
        {
            throw new DomainValidationException(nameof(Name), $"El nombre no puede superar {DeviceLimits.NameMaxLength} caracteres.");
        }

        return trimmed;
    }

    public void Rename(string? name, DateTimeOffset now)
    {
        Name = NormalizeName(name);
        UpdatedAt = now;
    }

    /// <summary>Nuevo código: el dispositivo vuelve a pendiente y el certificado anterior deja de valer.</summary>
    public string IssueClaimCode(DateTimeOffset now, TimeSpan validity)
    {
        var code = ClaimCode.Generate();
        ClaimCodeHash = ClaimCode.Hash(ClaimCode.Normalize(code)!);
        ClaimExpiresAt = now + validity;
        Status = DeviceStatus.Pending;
        CertificateThumbprint = null;
        CertificateExpiresAt = null;
        UpdatedAt = now;
        return code;
    }

    /// <summary>El certificado deja de valer y el código pendiente, si lo había, también.</summary>
    public void Revoke(DateTimeOffset now)
    {
        Status = DeviceStatus.Revoked;
        CertificateThumbprint = null;
        CertificateExpiresAt = null;
        ClaimCodeHash = null;
        ClaimExpiresAt = null;
        UpdatedAt = now;
    }

    /// <summary>Reemplaza los puntos que controla, conservando las filas que no cambian.</summary>
    public void AssignAccessPoints(IReadOnlyCollection<Guid> accessPointIds, DateTimeOffset now)
    {
        _accessPoints.RemoveAll(p => !accessPointIds.Contains(p.AccessPointId));
        foreach (var id in accessPointIds.Where(id => _accessPoints.All(p => p.AccessPointId != id)))
        {
            _accessPoints.Add(new DeviceAccessPoint(Id, id));
        }

        UpdatedAt = now;
    }

    public void RecordHeartbeat(string? firmwareVersion, int? policyVersion, DateTimeOffset now)
    {
        var firmware = string.IsNullOrWhiteSpace(firmwareVersion) ? null : firmwareVersion.Trim();
        if (firmware is { Length: > DeviceLimits.FirmwareVersionMaxLength })
        {
            throw new DomainValidationException(nameof(FirmwareVersion),
                $"La versión de firmware no puede superar {DeviceLimits.FirmwareVersionMaxLength} caracteres.");
        }

        if (policyVersion is < 0)
        {
            throw new DomainValidationException("PolicyVersion", "La versión de política no puede ser negativa.");
        }

        FirmwareVersion = firmware;
        ReportedPolicyVersion = policyVersion;
        LastSeenAt = now;
    }
}

/// <summary>
/// Versión de la política de un dispositivo (design, decisión 4): avanza de a uno cuando cambia la huella del contenido.
/// </summary>
public sealed class DevicePolicyState
{
    private DevicePolicyState()
    {
        ContentHash = string.Empty;
    }

    public Guid DeviceId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public int Version { get; private set; }

    /// <summary>SHA-256 (hex en minúsculas) del JSON canónico de los puntos.</summary>
    public string ContentHash { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }
}
