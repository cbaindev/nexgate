using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexgate.Common.BuildingBlocks.Persistence;
using Nexgate.Common.BuildingBlocks.Tenancy;
using Nexgate.Domain.Devices;
using Nexgate.Service.Devices.Contracts;
using Npgsql;

namespace Nexgate.Data.Devices;

/// <summary>Schema <c>devices</c> (design de add-device-sync-protocol, decisión 5).</summary>
public sealed class DevicesDbContext(DbContextOptions<DevicesDbContext> options, ICurrentOrganization currentOrganization)
    : NexgateDbContext(options, currentOrganization)
{
    public const string Schema = "devices";

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<DeviceAccessPoint> DeviceAccessPoints => Set<DeviceAccessPoint>();

    public DbSet<DevicePolicyState> PolicyStates => Set<DevicePolicyState>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Device>(device =>
        {
            device.ToTable("device");
            device.HasKey(d => d.Id);
            device.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
            device.IsTenantOwned();
            device.Property(d => d.SiteId).HasColumnName("site_id");
            device.Property(d => d.Name).HasColumnName("name").HasMaxLength(DeviceLimits.NameMaxLength).IsRequired();
            device.Property(d => d.Status).HasColumnName("status")
                .HasConversion(s => DeviceStatuses.ToName(s), s => DeviceStatuses.Parse(s))
                .HasMaxLength(20).IsRequired();
            device.Property(d => d.ClaimCodeHash).HasColumnName("claim_code_hash");
            device.Property(d => d.ClaimExpiresAt).HasColumnName("claim_expires_at");
            device.Property(d => d.CertificateThumbprint).HasColumnName("certificate_thumbprint").HasMaxLength(64);
            device.Property(d => d.CertificateExpiresAt).HasColumnName("certificate_expires_at");
            device.Property(d => d.FirmwareVersion).HasColumnName("firmware_version").HasMaxLength(DeviceLimits.FirmwareVersionMaxLength);
            device.Property(d => d.ReportedPolicyVersion).HasColumnName("reported_policy_version");
            device.Property(d => d.LastSeenAt).HasColumnName("last_seen_at");
            device.Property(d => d.CreatedBy).HasColumnName("created_by");
            device.Property(d => d.CreatedAt).HasColumnName("created_at");
            device.Property(d => d.UpdatedAt).HasColumnName("updated_at");
            device.HasIndex(d => d.OrganizationId).HasDatabaseName("ix_device_organization_id");
            device.HasIndex(d => d.SiteId).HasDatabaseName("ix_device_site_id");
            device.HasIndex(d => d.ClaimCodeHash).IsUnique().HasDatabaseName("ux_device_claim_code_hash");
            device.HasIndex(d => d.CertificateThumbprint).IsUnique().HasDatabaseName("ux_device_certificate_thumbprint");
            device.HasMany(d => d.AccessPoints).WithOne().HasForeignKey(p => p.DeviceId)
                .HasConstraintName("fk_device_access_point_device").OnDelete(DeleteBehavior.Cascade);
            device.Navigation(d => d.AccessPoints).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<DeviceAccessPoint>(point =>
        {
            point.ToTable("device_access_point");
            point.HasKey(p => new { p.DeviceId, p.AccessPointId });
            point.Property(p => p.DeviceId).HasColumnName("device_id");
            point.Property(p => p.AccessPointId).HasColumnName("access_point_id");
            point.IsTenantOwned();
            point.HasIndex(p => p.AccessPointId).IsUnique().HasDatabaseName("ux_device_access_point_access_point_id");
            point.HasIndex(p => p.OrganizationId).HasDatabaseName("ix_device_access_point_organization_id");
        });

        modelBuilder.Entity<DevicePolicyState>(state =>
        {
            state.ToTable("device_policy");
            state.HasKey(s => s.DeviceId);
            state.Property(s => s.DeviceId).HasColumnName("device_id").ValueGeneratedNever();
            state.IsTenantOwned();
            state.Property(s => s.Version).HasColumnName("version");
            state.Property(s => s.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
            state.Property(s => s.IssuedAt).HasColumnName("issued_at");
            state.HasOne<Device>().WithOne().HasForeignKey<DevicePolicyState>(s => s.DeviceId)
                .HasConstraintName("fk_device_policy_device").OnDelete(DeleteBehavior.Cascade);
            state.HasIndex(s => s.OrganizationId).HasDatabaseName("ix_device_policy_organization_id");
        });
    }
}

/// <summary>Un dispositivo pendiente con un código vigente (búsqueda previa al reclamo, sin organización activa).</summary>
public sealed record ClaimTarget(Guid DeviceId, Guid OrganizationId, Guid SiteId);

/// <summary>Persistencia de dispositivos del panel. Toda lectura y escritura está acotada a la organización activa.</summary>
public interface IDeviceStore
{
    Task<IReadOnlyList<Device>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken);

    Task<Device?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameInUseAsync(Guid siteId, string name, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Alguno de los puntos lo controla otro dispositivo.</summary>
    Task<bool> AnyAssignedElsewhereAsync(IReadOnlyCollection<Guid> accessPointIds, Guid deviceId, CancellationToken cancellationToken);

    Task<bool> AccessPointAssignedAsync(Guid accessPointId, CancellationToken cancellationToken);

    /// <summary>Versiones de política vigentes por dispositivo.</summary>
    Task<IReadOnlyDictionary<Guid, int>> PolicyVersionsAsync(IReadOnlyCollection<Guid> deviceIds, CancellationToken cancellationToken);

    void Add(Device device);

    void Remove(Device device);

    /// <summary>Guarda los cambios. Devuelve false si chocó con un índice único (nombre o punto ya asignado).</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Operaciones del protocolo de dispositivos. El reclamo y la autenticación usan funciones <c>SECURITY DEFINER</c>
/// (no hay organización activa todavía); el resto corre con la organización del dispositivo y RLS.
/// </summary>
public interface IDeviceProtocolStore
{
    Task<ClaimTarget?> FindClaimAsync(byte[] claimCodeHash, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Activa el dispositivo con su certificado si el código sigue vigente (un solo uso).</summary>
    Task<bool> CompleteClaimAsync(byte[] claimCodeHash, Guid deviceId, string thumbprint, DateTimeOffset certificateExpiresAt,
        DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Dispositivo activo con ese certificado vigente; registra el último contacto.</summary>
    Task<ClaimTarget?> AuthenticateAsync(string thumbprint, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> AccessPointIdsAsync(Guid deviceId, CancellationToken cancellationToken);

    Task<DevicePolicyState?> FindPolicyStateAsync(Guid deviceId, CancellationToken cancellationToken);

    /// <summary>
    /// Fija la versión siguiente con la nueva huella, solo si la vigente sigue siendo <paramref name="currentVersion"/>
    /// (0: no había). Devuelve false si otra solicitud se adelantó.
    /// </summary>
    Task<bool> AdvancePolicyAsync(Guid deviceId, Guid organizationId, int currentVersion, string contentHash, DateTimeOffset issuedAt,
        CancellationToken cancellationToken);

    Task<Device?> FindDeviceAsync(Guid deviceId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

internal sealed class DeviceStore(DevicesDbContext db) : IDeviceStore
{
    public async Task<IReadOnlyList<Device>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken) =>
        (await db.Devices.AsNoTracking().Include(d => d.AccessPoints).Where(d => d.SiteId == siteId).ToListAsync(cancellationToken))
        .OrderBy(d => d.Name, StringComparer.InvariantCultureIgnoreCase).ThenBy(d => d.Id).ToList();

    public Task<Device?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Devices.Include(d => d.AccessPoints).SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> NameInUseAsync(Guid siteId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var lower = name.ToLowerInvariant();
        return db.Devices.AnyAsync(d => d.SiteId == siteId && d.Name.ToLower() == lower && d.Id != exceptId, cancellationToken);
    }

    public Task<bool> AnyAssignedElsewhereAsync(IReadOnlyCollection<Guid> accessPointIds, Guid deviceId, CancellationToken cancellationToken) =>
        db.DeviceAccessPoints.AnyAsync(p => accessPointIds.Contains(p.AccessPointId) && p.DeviceId != deviceId, cancellationToken);

    public Task<bool> AccessPointAssignedAsync(Guid accessPointId, CancellationToken cancellationToken) =>
        db.DeviceAccessPoints.AnyAsync(p => p.AccessPointId == accessPointId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> PolicyVersionsAsync(IReadOnlyCollection<Guid> deviceIds, CancellationToken cancellationToken) =>
        await db.PolicyStates.AsNoTracking().Where(s => deviceIds.Contains(s.DeviceId)).ToDictionaryAsync(s => s.DeviceId, s => s.Version, cancellationToken);

    public void Add(Device device) => db.Devices.Add(device);

    public void Remove(Device device) => db.Devices.Remove(device);

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}

internal sealed class DeviceProtocolStore(DevicesDbContext db) : IDeviceProtocolStore
{
    private sealed record TargetRow(Guid DeviceId, Guid OrganizationId, Guid SiteId);

    public async Task<ClaimTarget?> FindClaimAsync(byte[] claimCodeHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = await db.Database.SqlQuery<TargetRow>($"""
                SELECT device_id AS "DeviceId", organization_id AS "OrganizationId", site_id AS "SiteId"
                FROM devices.find_claim({claimCodeHash}, {now})
                """)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new ClaimTarget(row.DeviceId, row.OrganizationId, row.SiteId);
    }

    public async Task<bool> CompleteClaimAsync(byte[] claimCodeHash, Guid deviceId, string thumbprint, DateTimeOffset certificateExpiresAt,
        DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<Guid>($"""
                SELECT device_id AS "Value" FROM devices.claim_device({claimCodeHash}, {deviceId}, {thumbprint}, {certificateExpiresAt}, {now})
                """)
            .AnyAsync(cancellationToken);

    public async Task<ClaimTarget?> AuthenticateAsync(string thumbprint, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = await db.Database.SqlQuery<TargetRow>($"""
                SELECT device_id AS "DeviceId", organization_id AS "OrganizationId", site_id AS "SiteId"
                FROM devices.authenticate_device({thumbprint}, {now})
                """)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new ClaimTarget(row.DeviceId, row.OrganizationId, row.SiteId);
    }

    public async Task<IReadOnlyList<Guid>> AccessPointIdsAsync(Guid deviceId, CancellationToken cancellationToken) =>
        await db.DeviceAccessPoints.AsNoTracking().Where(p => p.DeviceId == deviceId)
            .OrderBy(p => p.AccessPointId).Select(p => p.AccessPointId).ToListAsync(cancellationToken);

    public Task<DevicePolicyState?> FindPolicyStateAsync(Guid deviceId, CancellationToken cancellationToken) =>
        db.PolicyStates.AsNoTracking().SingleOrDefaultAsync(s => s.DeviceId == deviceId, cancellationToken);

    public async Task<bool> AdvancePolicyAsync(Guid deviceId, Guid organizationId, int currentVersion, string contentHash, DateTimeOffset issuedAt,
        CancellationToken cancellationToken)
    {
        var next = currentVersion + 1;
        var rows = currentVersion == 0
            ? await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO devices.device_policy (device_id, organization_id, version, content_hash, issued_at)
                VALUES ({deviceId}, {organizationId}, {next}, {contentHash}, {issuedAt})
                ON CONFLICT (device_id) DO NOTHING
                """, cancellationToken)
            : await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE devices.device_policy SET version = {next}, content_hash = {contentHash}, issued_at = {issuedAt}
                WHERE device_id = {deviceId} AND version = {currentVersion}
                """, cancellationToken);
        return rows == 1;
    }

    public Task<Device?> FindDeviceAsync(Guid deviceId, CancellationToken cancellationToken) =>
        db.Devices.SingleOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// RLS y funciones restringidas del schema <c>devices</c> (design, decisiones 2, 3 y 5). Las usa la migración inicial; las
/// pruebas las verifican contra la base real.
/// </summary>
public static class DevicesIsolationSql
{
    /// <summary>Funciones <c>SECURITY DEFINER</c> conocidas del schema.</summary>
    public static readonly IReadOnlyList<string> SecurityDefinerFunctions =
        ["devices.authenticate_device", "devices.claim_device", "devices.find_claim"];

    private const string Owner = DatabaseRoles.Owner;
    private const string App = DatabaseRoles.App;

    public static IReadOnlyList<string> Up =>
    [
        .. TenantIsolationMigrationExtensions.TenantIsolationSql("devices", "device"),
        .. TenantIsolationMigrationExtensions.TenantIsolationSql("devices", "device_access_point"),
        .. TenantIsolationMigrationExtensions.TenantIsolationSql("devices", "device_policy"),

        // Con FORCE, RLS también aplica al dueño. Las funciones restringidas corren como dueño y leen o actualizan una
        // fila por clave exacta (hash del código o huella del certificado), nunca un listado.
        $"CREATE POLICY owner_lookup ON devices.device FOR SELECT TO {Owner} USING (true);",
        $"CREATE POLICY owner_update ON devices.device FOR UPDATE TO {Owner} USING (true) WITH CHECK (true);",
        """
        CREATE FUNCTION devices.find_claim(p_claim_code_hash bytea, p_now timestamptz)
            RETURNS TABLE (device_id uuid, organization_id uuid, site_id uuid)
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, pg_temp
            AS $$
                SELECT d.id, d.organization_id, d.site_id
                FROM devices.device d
                WHERE d.claim_code_hash = p_claim_code_hash AND d.status = 'pending' AND d.claim_expires_at > p_now
            $$;
        """,
        """
        CREATE FUNCTION devices.claim_device(p_claim_code_hash bytea, p_device_id uuid, p_thumbprint text,
                                             p_certificate_expires_at timestamptz, p_now timestamptz)
            RETURNS TABLE (device_id uuid)
            LANGUAGE sql VOLATILE SECURITY DEFINER
            SET search_path = pg_catalog, pg_temp
            AS $$
                UPDATE devices.device d
                SET status = 'active', claim_code_hash = NULL, claim_expires_at = NULL,
                    certificate_thumbprint = p_thumbprint, certificate_expires_at = p_certificate_expires_at,
                    last_seen_at = p_now, updated_at = p_now
                WHERE d.id = p_device_id AND d.claim_code_hash = p_claim_code_hash AND d.status = 'pending'
                  AND d.claim_expires_at > p_now
                RETURNING d.id
            $$;
        """,
        """
        CREATE FUNCTION devices.authenticate_device(p_thumbprint text, p_now timestamptz)
            RETURNS TABLE (device_id uuid, organization_id uuid, site_id uuid)
            LANGUAGE sql VOLATILE SECURITY DEFINER
            SET search_path = pg_catalog, pg_temp
            AS $$
                UPDATE devices.device d
                SET last_seen_at = p_now
                WHERE d.certificate_thumbprint = p_thumbprint AND d.status = 'active'
                  AND d.certificate_expires_at > p_now
                RETURNING d.id, d.organization_id, d.site_id
            $$;
        """,
        "REVOKE ALL ON FUNCTION devices.find_claim(bytea, timestamptz) FROM PUBLIC;",
        "REVOKE ALL ON FUNCTION devices.claim_device(bytea, uuid, text, timestamptz, timestamptz) FROM PUBLIC;",
        "REVOKE ALL ON FUNCTION devices.authenticate_device(text, timestamptz) FROM PUBLIC;",
        $"GRANT EXECUTE ON FUNCTION devices.find_claim(bytea, timestamptz) TO {App};",
        $"GRANT EXECUTE ON FUNCTION devices.claim_device(bytea, uuid, text, timestamptz, timestamptz) TO {App};",
        $"GRANT EXECUTE ON FUNCTION devices.authenticate_device(text, timestamptz) TO {App};"
    ];

    public static IReadOnlyList<string> Down =>
    [
        "DROP FUNCTION IF EXISTS devices.authenticate_device(text, timestamptz);",
        "DROP FUNCTION IF EXISTS devices.claim_device(bytea, uuid, text, timestamptz, timestamptz);",
        "DROP FUNCTION IF EXISTS devices.find_claim(bytea, timestamptz);"
    ];
}

public static class DevicesDataServiceCollectionExtensions
{
    /// <summary>Persistencia de Devices (schema <c>devices</c>). La registra <c>AddDevicesService</c>.</summary>
    public static IServiceCollection AddDevicesData(this IServiceCollection services)
    {
        services.AddModuleDbContext<DevicesDbContext>(DevicesDbContext.Schema);
        services.AddScoped<IDeviceStore, DeviceStore>();
        services.AddScoped<IDeviceProtocolStore, DeviceProtocolStore>();
        return services;
    }
}
