using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexgate.Common.BuildingBlocks.Persistence;
using Nexgate.Common.BuildingBlocks.Tenancy;
using Nexgate.Domain.Events;

namespace Nexgate.Data.Events;

/// <summary>Schema <c>events</c> (design de add-device-sync-protocol, decisión 6). El particionado mensual llega con la retención.</summary>
public sealed class EventsDbContext(DbContextOptions<EventsDbContext> options, ICurrentOrganization currentOrganization)
    : NexgateDbContext(options, currentOrganization)
{
    public const string Schema = "events";
    public const string DeviceEventIndex = "ux_access_event_device_event";

    public DbSet<AccessEvent> AccessEvents => Set<AccessEvent>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<AccessEvent>(e =>
        {
            e.ToTable("access_event");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.IsTenantOwned();
            e.Property(x => x.SiteId).HasColumnName("site_id");
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.DeviceEventId).HasColumnName("device_event_id");
            e.Property(x => x.AccessPointId).HasColumnName("access_point_id");
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            e.Property(x => x.ReceivedAt).HasColumnName("received_at");
            e.Property(x => x.Decision).HasColumnName("decision").HasMaxLength(20).IsRequired();
            e.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(40).IsRequired();
            e.Property(x => x.CredentialId).HasColumnName("credential_id");
            e.Property(x => x.SubjectId).HasColumnName("subject_id");
            e.Property(x => x.PolicyVersion).HasColumnName("policy_version");
            e.Property(x => x.UsesSettled).HasColumnName("uses_settled");
            e.HasIndex(x => new { x.DeviceId, x.DeviceEventId }).IsUnique().HasDatabaseName(DeviceEventIndex);
            e.HasIndex(x => new { x.SiteId, x.OccurredAt, x.Id }).IsDescending(false, true, true).HasDatabaseName("ix_access_event_site_occurred");
            e.HasIndex(x => x.OrganizationId).HasDatabaseName("ix_access_event_organization_id");
        });
    }
}

public sealed record EventFilter(Guid SiteId, Guid? AccessPointId, Guid? SubjectId, DateTimeOffset? From, DateTimeOffset? To,
    DateTimeOffset? AfterOccurredAt, Guid? AfterId, int Take);

/// <summary>Persistencia de eventos. Toda lectura y escritura está acotada a la organización activa.</summary>
public interface IAccessEventStore
{
    /// <summary>Inserta el evento si el dispositivo no lo había enviado antes. Devuelve false si ya existía.</summary>
    Task<bool> TryInsertAsync(AccessEvent accessEvent, CancellationToken cancellationToken);

    /// <summary>Eventos ya guardados de ese dispositivo con descuento de usos pendiente (reintento tras un reenvío).</summary>
    Task<IReadOnlyList<AccessEvent>> ListUnsettledAsync(Guid deviceId, IReadOnlyCollection<Guid> deviceEventIds, CancellationToken cancellationToken);

    Task MarkSettledAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Del más reciente al más antiguo por (instante, id), a partir del cursor.</summary>
    Task<IReadOnlyList<AccessEvent>> ListAsync(EventFilter filter, CancellationToken cancellationToken);
}

internal sealed class AccessEventStore(EventsDbContext db) : IAccessEventStore
{
    public async Task<bool> TryInsertAsync(AccessEvent e, CancellationToken cancellationToken)
    {
        var organization = db.CurrentOrganizationId
            ?? throw new TenantIsolationException("No se puede registrar un evento sin organización activa.");
        return await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO events.access_event (id, organization_id, site_id, device_id, device_event_id, access_point_id, occurred_at,
                received_at, decision, reason, credential_id, subject_id, policy_version, uses_settled)
            VALUES ({e.Id}, {organization}, {e.SiteId}, {e.DeviceId}, {e.DeviceEventId}, {e.AccessPointId}, {e.OccurredAt},
                {e.ReceivedAt}, {e.Decision}, {e.Reason}, {e.CredentialId}, {e.SubjectId}, {e.PolicyVersion}, {e.UsesSettled})
            ON CONFLICT (device_id, device_event_id) DO NOTHING
            """, cancellationToken) == 1;
    }

    public async Task<IReadOnlyList<AccessEvent>> ListUnsettledAsync(Guid deviceId, IReadOnlyCollection<Guid> deviceEventIds,
        CancellationToken cancellationToken) =>
        await db.AccessEvents.AsNoTracking()
            .Where(e => e.DeviceId == deviceId && deviceEventIds.Contains(e.DeviceEventId) && !e.UsesSettled)
            .ToListAsync(cancellationToken);

    public Task MarkSettledAsync(Guid id, CancellationToken cancellationToken) =>
        db.AccessEvents.Where(e => e.Id == id).ExecuteUpdateAsync(s => s.SetProperty(e => e.UsesSettled, true), cancellationToken);

    public async Task<IReadOnlyList<AccessEvent>> ListAsync(EventFilter f, CancellationToken cancellationToken) =>
        await db.AccessEvents.FromSqlInterpolated($"""
                SELECT * FROM events.access_event
                WHERE site_id = {f.SiteId}
                  AND ({f.AccessPointId}::uuid IS NULL OR access_point_id = {f.AccessPointId})
                  AND ({f.SubjectId}::uuid IS NULL OR subject_id = {f.SubjectId})
                  AND ({f.From}::timestamptz IS NULL OR occurred_at >= {f.From})
                  AND ({f.To}::timestamptz IS NULL OR occurred_at < {f.To})
                  AND ({f.AfterOccurredAt}::timestamptz IS NULL OR (occurred_at, id) < ({f.AfterOccurredAt}, {f.AfterId}::uuid))
                """)
            .AsNoTracking()
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .Take(f.Take)
            .ToListAsync(cancellationToken);
}

public static class EventsDataServiceCollectionExtensions
{
    /// <summary>Persistencia de Events (schema <c>events</c>). La registra <c>AddEventsService</c>.</summary>
    public static IServiceCollection AddEventsData(this IServiceCollection services)
    {
        services.AddModuleDbContext<EventsDbContext>(EventsDbContext.Schema);
        services.AddScoped<IAccessEventStore, AccessEventStore>();
        return services;
    }
}
