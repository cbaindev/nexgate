using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Nexgate.Common.BuildingBlocks.Persistence;
using Nexgate.Common.BuildingBlocks.Tenancy;
using Nexgate.Domain.Access;
using Nexgate.Service.Access.Contracts;

namespace Nexgate.Data.Access;

/// <summary>
/// Schema <c>access</c> (design de add-access-evaluator, decisiones 2 a 4 y 10). Sujetos, espacios y puntos se validan
/// por contrato: no hay claves foráneas entre schemas.
/// </summary>
public sealed class AccessDbContext(DbContextOptions<AccessDbContext> options, ICurrentOrganization currentOrganization)
    : NexgateDbContext(options, currentOrganization)
{
    public const string Schema = "access";
    public const string ScheduleNameIndex = "ux_schedule_site_name";
    public const string CredentialValueIndex = "ux_credential_site_type_hash";
    public const string GrantScheduleForeignKey = "fk_grant_schedule";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public DbSet<Schedule> Schedules => Set<Schedule>();

    public DbSet<Credential> Credentials => Set<Credential>();

    public DbSet<Grant> Grants => Set<Grant>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Schedule>(schedule =>
        {
            schedule.ToTable("schedule");
            schedule.HasKey(s => s.Id);
            schedule.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            schedule.IsTenantOwned();
            schedule.Property(s => s.SiteId).HasColumnName("site_id");
            schedule.Property(s => s.Name).HasColumnName("name").HasMaxLength(AccessLimits.ScheduleNameMaxLength).IsRequired();
            schedule.Property(s => s.Windows).HasColumnName("windows").HasColumnType("jsonb")
                .HasConversion(
                    w => JsonSerializer.Serialize(w, Json),
                    w => JsonSerializer.Deserialize<List<ScheduleWindow>>(w, Json)!,
                    new ValueComparer<IReadOnlyList<ScheduleWindow>>(
                        (a, b) => a!.SequenceEqual(b!),
                        w => w.Aggregate(0, (h, x) => HashCode.Combine(h, x)),
                        w => w.ToList()))
                .IsRequired();
            schedule.Property(s => s.CreatedAt).HasColumnName("created_at");
            schedule.Property(s => s.UpdatedAt).HasColumnName("updated_at");
            schedule.HasIndex(s => s.OrganizationId).HasDatabaseName("ix_schedule_organization_id");
            schedule.HasIndex(s => s.SiteId).HasDatabaseName("ix_schedule_site_id");
        });

        modelBuilder.Entity<Credential>(credential =>
        {
            credential.ToTable("credential");
            credential.HasKey(c => c.Id);
            credential.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            credential.IsTenantOwned();
            credential.Property(c => c.SiteId).HasColumnName("site_id");
            credential.Property(c => c.SubjectId).HasColumnName("subject_id");
            credential.Property(c => c.Type).HasColumnName("type")
                .HasConversion(t => AccessNames.ToName(t), t => ParseCredentialType(t)).HasMaxLength(20).IsRequired();
            credential.Property(c => c.ValueHash).HasColumnName("value_hash").IsRequired();
            credential.Property(c => c.ValidFrom).HasColumnName("valid_from");
            credential.Property(c => c.ValidUntil).HasColumnName("valid_until");
            credential.Property(c => c.CreatedAt).HasColumnName("created_at");
            credential.HasIndex(c => c.OrganizationId).HasDatabaseName("ix_credential_organization_id");
            credential.HasIndex(c => new { c.SiteId, c.Type, c.ValueHash }).IsUnique().HasDatabaseName(CredentialValueIndex);
            credential.HasIndex(c => c.SubjectId).HasDatabaseName("ix_credential_subject_id");
        });

        modelBuilder.Entity<Grant>(grant =>
        {
            grant.ToTable("grant");
            grant.HasKey(g => g.Id);
            grant.Property(g => g.Id).HasColumnName("id").ValueGeneratedNever();
            grant.IsTenantOwned();
            grant.Property(g => g.SiteId).HasColumnName("site_id");
            grant.Property(g => g.SubjectId).HasColumnName("subject_id");
            grant.Property(g => g.TargetType).HasColumnName("target_type")
                .HasConversion(t => AccessNames.ToName(t), t => ParseTargetType(t)).HasMaxLength(20).IsRequired();
            grant.Property(g => g.TargetId).HasColumnName("target_id");
            grant.Property(g => g.ScheduleId).HasColumnName("schedule_id");
            grant.Property(g => g.ValidFrom).HasColumnName("valid_from");
            grant.Property(g => g.ValidUntil).HasColumnName("valid_until");
            grant.Property(g => g.Effect).HasColumnName("effect")
                .HasConversion(e => AccessNames.ToName(e), e => e == GrantEffectNames.Deny ? GrantEffect.Deny : GrantEffect.Allow)
                .HasMaxLength(10).IsRequired();
            grant.Property(g => g.Origin).HasColumnName("origin_type")
                .HasConversion(o => AccessNames.ToName(o), _ => GrantOrigin.Manual).HasMaxLength(20).IsRequired();
            grant.Property(g => g.OriginId).HasColumnName("origin_id");
            grant.Property(g => g.RemainingUses).HasColumnName("remaining_uses");
            grant.Property(g => g.CreatedBy).HasColumnName("created_by");
            grant.Property(g => g.CreatedAt).HasColumnName("created_at");
            grant.HasIndex(g => g.OrganizationId).HasDatabaseName("ix_grant_organization_id");
            grant.HasIndex(g => new { g.SiteId, g.TargetType, g.TargetId }).HasDatabaseName("ix_grant_site_target");
            grant.HasIndex(g => g.SubjectId).HasDatabaseName("ix_grant_subject_id");
            grant.HasOne<Schedule>().WithMany().HasForeignKey(g => g.ScheduleId)
                .HasConstraintName(GrantScheduleForeignKey).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static CredentialType ParseCredentialType(string value) =>
        AccessNames.TryParseCredentialType(value, out var type) ? type : throw new InvalidOperationException("Tipo de credencial desconocido en la base.");

    private static GrantTargetType ParseTargetType(string value) =>
        AccessNames.TryParseTargetType(value, out var type) ? type : throw new InvalidOperationException("Tipo de destino desconocido en la base.");
}
