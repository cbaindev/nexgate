using Microsoft.EntityFrameworkCore;
using Nexgate.BuildingBlocks.Persistence;
using Nexgate.BuildingBlocks.Tenancy;
using Nexgate.Modules.Tenancy.Application;
using Nexgate.Modules.Tenancy.Domain;

namespace Nexgate.Modules.Tenancy.Infrastructure;

/// <summary>Schema <c>tenancy</c>. Las políticas RLS están en la migración inicial (design, decisión 4).</summary>
public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options, ICurrentOrganization currentOrganization)
    : NexgateDbContext(options, currentOrganization)
{
    public const string Schema = "tenancy";
    public const string SiteNameIndex = "ux_site_organization_name";

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Site> Sites => Set<Site>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Organization>(organization =>
        {
            organization.ToTable("organization");
            organization.HasKey(o => o.Id);
            organization.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
            organization.Property(o => o.Name).HasColumnName("name").HasMaxLength(Organization.NameMaxLength).IsRequired();
            organization.Property(o => o.Status).HasColumnName("status")
                .HasConversion(s => s.ToString().ToLowerInvariant(), s => Enum.Parse<OrganizationStatus>(s, true))
                .HasMaxLength(20).IsRequired();
            organization.Property(o => o.CreatedAt).HasColumnName("created_at");

            // La organización es la raíz: su id cumple el papel de organization_id (RLS por id).
            organization.HasQueryFilter(o => (Guid?)o.Id == CurrentOrganizationId);
        });

        modelBuilder.Entity<Site>(site =>
        {
            site.ToTable("site");
            site.HasKey(s => s.Id);
            site.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            site.IsTenantOwned();
            site.Property(s => s.Name).HasColumnName("name").HasMaxLength(Site.NameMaxLength).IsRequired();
            site.Property(s => s.Type).HasColumnName("type")
                .HasConversion(t => SiteTypes.ToName(t), t => ParseType(t))
                .HasMaxLength(30).IsRequired();
            site.Property(s => s.TimeZone).HasColumnName("time_zone").HasMaxLength(Site.TimeZoneMaxLength).IsRequired();
            site.Property(s => s.Configuration).HasColumnName("configuration").HasColumnType("jsonb").IsRequired();
            site.Property(s => s.CreatedAt).HasColumnName("created_at");
            site.Property(s => s.UpdatedAt).HasColumnName("updated_at");
            site.HasIndex(s => s.OrganizationId).HasDatabaseName("ix_site_organization_id");
            site.HasOne<Organization>().WithMany().HasForeignKey(s => s.OrganizationId)
                .HasConstraintName("fk_site_organization").OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static SiteType ParseType(string value) =>
        SiteTypes.TryParse(value, out var type) ? type : throw new InvalidOperationException("Tipo de sitio desconocido en la base.");
}
