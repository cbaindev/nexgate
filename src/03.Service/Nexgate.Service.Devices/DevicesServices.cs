using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Nexgate.Data.Devices;
using Nexgate.Domain.Devices;
using Nexgate.Service.Access.Contracts;
using Nexgate.Service.Devices.Contracts;
using Nexgate.Service.Spaces.Contracts;
using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Service.Devices;

internal static class DevicesMapping
{
    public static DeviceDto ToDto(Device device, int? policyVersion) =>
        new(device.Id, device.SiteId, device.Name, DeviceStatuses.ToName(device.Status),
            device.AccessPoints.Select(p => p.AccessPointId).Order().ToList(),
            device.LastSeenAt, device.FirmwareVersion, device.ReportedPolicyVersion, policyVersion,
            device.CertificateExpiresAt, device.ClaimExpiresAt);
}

/// <summary>Dispositivos del panel (design de add-device-sync-protocol, decisión 5).</summary>
internal sealed class DeviceService(
    IDeviceStore store,
    ISiteDirectory sites,
    ISpaceDirectory spaces,
    IOptions<DevicesOptions> options,
    TimeProvider clock) : IDeviceService
{
    public const int MaxAccessPoints = 50;

    private static readonly DevicesError NameConflict = new(DevicesErrorKind.Conflict, null,
        "Ya hay un dispositivo con ese nombre en el sitio.", DevicesErrorCodes.DeviceNameConflict);

    private static readonly DevicesError PointAssigned = new(DevicesErrorKind.Conflict, null,
        "Alguno de los puntos de acceso ya lo controla otro dispositivo.", DevicesErrorCodes.AccessPointAssigned);

    private TimeSpan ClaimValidity => TimeSpan.FromDays(options.Value.ClaimCodeDays);

    public async Task<DevicesResult<IReadOnlyList<DeviceDto>>> ListAsync(Guid siteId, CancellationToken cancellationToken)
    {
        if (!await sites.ExistsAsync(siteId, cancellationToken))
        {
            return DevicesResult<IReadOnlyList<DeviceDto>>.NotFound();
        }

        var devices = await store.ListBySiteAsync(siteId, cancellationToken);
        var versions = await store.PolicyVersionsAsync(devices.Select(d => d.Id).ToList(), cancellationToken);
        return DevicesResult<IReadOnlyList<DeviceDto>>.Ok(
            devices.Select(d => DevicesMapping.ToDto(d, versions.TryGetValue(d.Id, out var v) ? v : null)).ToList());
    }

    public async Task<DevicesResult<DeviceDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await store.FindAsync(id, cancellationToken) is { } device
            ? DevicesResult<DeviceDto>.Ok(await ToDtoAsync(device, cancellationToken))
            : DevicesResult<DeviceDto>.NotFound();

    public async Task<DevicesResult<IssuedDevice>> CreateAsync(Guid siteId, string name, Guid createdBy, CancellationToken cancellationToken)
    {
        if (!await sites.ExistsAsync(siteId, cancellationToken))
        {
            return DevicesResult<IssuedDevice>.NotFound();
        }

        Device device;
        string code;
        try
        {
            var normalized = Device.NormalizeName(name);
            if (await store.NameInUseAsync(siteId, normalized, null, cancellationToken))
            {
                return DevicesResult<IssuedDevice>.Fail(NameConflict);
            }

            (device, code) = Device.Create(siteId, normalized, createdBy, clock.GetUtcNow(), ClaimValidity);
        }
        catch (DomainValidationException e)
        {
            return DevicesResult<IssuedDevice>.Invalid(e.Field, e.Message);
        }

        store.Add(device);
        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return DevicesResult<IssuedDevice>.Fail(NameConflict);
        }

        return DevicesResult<IssuedDevice>.Ok(new IssuedDevice(DevicesMapping.ToDto(device, null), code, device.ClaimExpiresAt!.Value));
    }

    public async Task<DevicesResult<DeviceDto>> RenameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } device)
        {
            return DevicesResult<DeviceDto>.NotFound();
        }

        try
        {
            var normalized = Device.NormalizeName(name);
            if (await store.NameInUseAsync(device.SiteId, normalized, device.Id, cancellationToken))
            {
                return DevicesResult<DeviceDto>.Fail(NameConflict);
            }

            device.Rename(normalized, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return DevicesResult<DeviceDto>.Invalid(e.Field, e.Message);
        }

        return await store.SaveChangesAsync(cancellationToken)
            ? DevicesResult<DeviceDto>.Ok(await ToDtoAsync(device, cancellationToken))
            : DevicesResult<DeviceDto>.Fail(NameConflict);
    }

    public async Task<DevicesResult<DeviceDto>> AssignAsync(Guid id, IReadOnlyList<Guid> accessPointIds, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } device)
        {
            return DevicesResult<DeviceDto>.NotFound();
        }

        var ids = accessPointIds.Distinct().ToList();
        if (ids.Count > MaxAccessPoints)
        {
            return DevicesResult<DeviceDto>.Invalid("AccessPointIds", $"Un dispositivo controla hasta {MaxAccessPoints} puntos de acceso.");
        }

        foreach (var pointId in ids)
        {
            // Lo de otro sitio, de otra organización o inexistente responde igual.
            if (await spaces.GetAccessPointAsync(pointId, cancellationToken) is not { } point || point.SiteId != device.SiteId)
            {
                return DevicesResult<DeviceDto>.Invalid("AccessPointIds", "Algún punto de acceso no existe en el sitio del dispositivo.");
            }
        }

        if (await store.AnyAssignedElsewhereAsync(ids, device.Id, cancellationToken))
        {
            return DevicesResult<DeviceDto>.Fail(PointAssigned);
        }

        device.AssignAccessPoints(ids, clock.GetUtcNow());
        return await store.SaveChangesAsync(cancellationToken)
            ? DevicesResult<DeviceDto>.Ok(await ToDtoAsync(device, cancellationToken))
            : DevicesResult<DeviceDto>.Fail(PointAssigned);
    }

    public async Task<DevicesResult<IssuedDevice>> IssueClaimCodeAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } device)
        {
            return DevicesResult<IssuedDevice>.NotFound();
        }

        var code = device.IssueClaimCode(clock.GetUtcNow(), ClaimValidity);
        await store.SaveChangesAsync(cancellationToken);
        return DevicesResult<IssuedDevice>.Ok(new IssuedDevice(await ToDtoAsync(device, cancellationToken), code, device.ClaimExpiresAt!.Value));
    }

    public async Task<DevicesResult<DeviceDto>> RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } device)
        {
            return DevicesResult<DeviceDto>.NotFound();
        }

        device.Revoke(clock.GetUtcNow());
        await store.SaveChangesAsync(cancellationToken);
        return DevicesResult<DeviceDto>.Ok(await ToDtoAsync(device, cancellationToken));
    }

    public async Task<DevicesResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } device)
        {
            return DevicesResult<Guid>.NotFound();
        }

        store.Remove(device);
        await store.SaveChangesAsync(cancellationToken);
        return DevicesResult<Guid>.Ok(id);
    }

    private async Task<DeviceDto> ToDtoAsync(Device device, CancellationToken cancellationToken)
    {
        var versions = await store.PolicyVersionsAsync([device.Id], cancellationToken);
        return DevicesMapping.ToDto(device, versions.TryGetValue(device.Id, out var v) ? v : null);
    }
}

/// <summary>
/// Reclamo de un dispositivo (design, decisión 2): código de un solo uso y CSR P-256 → certificado de cliente, clave del
/// sitio y clave pública de las políticas. No hay organización activa: las búsquedas usan funciones restringidas.
/// </summary>
internal sealed class DeviceClaimService(
    IDeviceProtocolStore store,
    DeviceCertificateAuthority authority,
    PolicySigningKey signing,
    ISiteCredentialKeys siteKeys,
    IOptions<DevicesOptions> options,
    TimeProvider clock) : IDeviceClaimService
{
    public async Task<DevicesResult<DeviceClaim>> ClaimAsync(string? claimCode, string? certificateRequest, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var normalized = ClaimCode.Normalize(claimCode);
        if (normalized is null)
        {
            return InvalidClaim();
        }

        var hash = ClaimCode.Hash(normalized);
        if (await store.FindClaimAsync(hash, now, cancellationToken) is not { } target)
        {
            return InvalidClaim();
        }

        string certificatePem;
        string thumbprint;
        DateTimeOffset expiresAt;
        try
        {
            using var certificate = DeviceCertificates.Issue(certificateRequest, target.DeviceId, authority.Certificate, now,
                TimeSpan.FromDays(options.Value.CertificateValidityDays));
            certificatePem = certificate.ExportCertificatePem();
            thumbprint = DeviceCertificates.Thumbprint(certificate);
            expiresAt = new DateTimeOffset(certificate.NotAfter.ToUniversalTime(), TimeSpan.Zero);
        }
        catch (DomainValidationException e)
        {
            // El código sigue vigente: el dispositivo puede reintentar con una solicitud válida.
            return DevicesResult<DeviceClaim>.Invalid(e.Field, e.Message);
        }

        if (!await store.CompleteClaimAsync(hash, target.DeviceId, thumbprint, expiresAt, now, cancellationToken))
        {
            return InvalidClaim();
        }

        return DevicesResult<DeviceClaim>.Ok(new DeviceClaim(
            target.DeviceId,
            target.SiteId,
            certificatePem,
            authority.CertificatePem,
            Convert.ToBase64String(siteKeys.GetSiteKey(target.SiteId)),
            signing.PublicKeyPem,
            options.Value.PollSeconds));
    }

    // Inexistente, usado, vencido o de un dispositivo que ya no está pendiente: la misma respuesta.
    private static DevicesResult<DeviceClaim> InvalidClaim() =>
        DevicesResult<DeviceClaim>.Invalid("ClaimCode", "El código de reclamo no es válido o venció.", DevicesErrorCodes.InvalidClaim);
}

internal sealed class DeviceAuthenticator(IDeviceProtocolStore store, TimeProvider clock) : IDeviceAuthenticator
{
    public async Task<DeviceIdentity?> AuthenticateAsync(byte[] certificate, CancellationToken cancellationToken) =>
        await store.AuthenticateAsync(DeviceCertificates.Thumbprint(certificate), clock.GetUtcNow(), cancellationToken) is { } target
            ? new DeviceIdentity(target.DeviceId, target.OrganizationId, target.SiteId)
            : null;
}

/// <summary>
/// Paquete de política por dispositivo, versión monotónica y latido (design, decisión 4). La política se compila al
/// consultarla; la versión solo avanza cuando cambia la huella del contenido.
/// </summary>
internal sealed class DevicePolicyService(
    IDeviceProtocolStore store,
    IAccessPolicyService policies,
    PolicySigningKey signing,
    IOptions<DevicesOptions> options,
    TimeProvider clock) : IDevicePolicyService
{
    public const int Format = 1;

    internal static readonly JsonSerializerOptions Canonical = new(JsonSerializerDefaults.Web);

    /// <summary>Contenido del paquete, en este orden. Los bytes serializados son lo que se firma.</summary>
    internal sealed record PackageDocument(int Format, Guid DeviceId, Guid SiteId, int Version, DateTimeOffset IssuedAt,
        IReadOnlyList<CompiledPolicy> Points);

    public async Task<DevicesResult<DevicePolicyPackage>> GetPolicyAsync(DeviceIdentity device, CancellationToken cancellationToken)
    {
        var (points, state) = await CurrentAsync(device, cancellationToken);
        var content = JsonSerializer.SerializeToUtf8Bytes(
            new PackageDocument(Format, device.DeviceId, device.SiteId, state.Version, state.IssuedAt, points), Canonical);
        return DevicesResult<DevicePolicyPackage>.Ok(new DevicePolicyPackage(state.Version, content, PolicySignature.Sign(signing.Key, content)));
    }

    public async Task<DevicesResult<HeartbeatResult>> HeartbeatAsync(DeviceIdentity device, HeartbeatInput input, CancellationToken cancellationToken)
    {
        if (await store.FindDeviceAsync(device.DeviceId, cancellationToken) is not { } entity)
        {
            return DevicesResult<HeartbeatResult>.NotFound();
        }

        var now = clock.GetUtcNow();
        try
        {
            entity.RecordHeartbeat(input.FirmwareVersion, input.PolicyVersion, now);
        }
        catch (DomainValidationException e)
        {
            return DevicesResult<HeartbeatResult>.Invalid(e.Field, e.Message);
        }

        await store.SaveChangesAsync(cancellationToken);
        var (_, state) = await CurrentAsync(device, cancellationToken);
        return DevicesResult<HeartbeatResult>.Ok(new HeartbeatResult(state.Version, now, options.Value.PollSeconds));
    }

    private async Task<(IReadOnlyList<CompiledPolicy> Points, (int Version, DateTimeOffset IssuedAt) State)> CurrentAsync(
        DeviceIdentity device, CancellationToken cancellationToken)
    {
        var points = new List<CompiledPolicy>();
        foreach (var pointId in await store.AccessPointIdsAsync(device.DeviceId, cancellationToken))
        {
            if ((await policies.CompileAsync(pointId, cancellationToken)).Value is { } policy)
            {
                points.Add(policy);
            }
        }

        var hash = ContentHash(points);
        for (var attempt = 0; ; attempt++)
        {
            var state = await store.FindPolicyStateAsync(device.DeviceId, cancellationToken);
            if (state is not null && state.ContentHash == hash)
            {
                return (points, (state.Version, state.IssuedAt));
            }

            // Segundos enteros: el instante guardado y el servido son idénticos (la base no guarda ticks).
            var now = clock.GetUtcNow();
            var issuedAt = new DateTimeOffset(now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
            var current = state?.Version ?? 0;
            if (await store.AdvancePolicyAsync(device.DeviceId, device.OrganizationId, current, hash, issuedAt, cancellationToken))
            {
                return (points, (current + 1, issuedAt));
            }

            if (attempt >= 3)
            {
                throw new InvalidOperationException("No se pudo fijar la versión de la política del dispositivo.");
            }
        }
    }

    /// <summary>SHA-256 (hex en minúsculas) del JSON canónico de las políticas de los puntos.</summary>
    internal static string ContentHash(IReadOnlyList<CompiledPolicy> points) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(points, Canonical))));
}

/// <summary>Un punto de acceso asignado a un dispositivo no se elimina.</summary>
internal sealed class DeviceAccessPointUsage(IDeviceStore store) : IAccessPointUsage
{
    public Task<bool> IsUsedAsync(Guid accessPointId, CancellationToken cancellationToken) =>
        store.AccessPointAssignedAsync(accessPointId, cancellationToken);
}

/// <summary>
/// Módulo Devices: alta, reclamo con certificado de cliente, política firmada por dispositivo y latido. Requiere los
/// módulos Tenancy, Spaces y Access registrados.
/// </summary>
public static class DevicesServiceCollectionExtensions
{
    public static IServiceCollection AddDevicesService(this IServiceCollection services)
    {
        services.AddDevicesData();
        services.AddOptions<DevicesOptions>().BindConfiguration(DevicesOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<DevicesOptions>, DevicesOptionsValidator>();
        services.AddSingleton<DeviceCertificateAuthority>();
        services.AddSingleton<PolicySigningKey>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IDeviceClaimService, DeviceClaimService>();
        services.AddScoped<IDeviceAuthenticator, DeviceAuthenticator>();
        services.AddScoped<IDevicePolicyService, DevicePolicyService>();
        services.AddScoped<IAccessPointUsage, DeviceAccessPointUsage>();
        return services;
    }
}
