using Microsoft.EntityFrameworkCore;
using Nexgate.Common.BuildingBlocks.Persistence;
using Nexgate.Common.BuildingBlocks.Tenancy;
using Nexgate.Domain.Spaces;
using Nexgate.Service.Spaces.Contracts;

namespace Nexgate.Data.Spaces;

/// <summary>
/// Schema <c>spaces</c> (design de add-spaces-and-access-points, decisión 3). Las claves compuestas con
/// <c>site_id</c> garantizan en la base que el padre, el destino y el origen son del mismo sitio; las restricciones
/// <c>ON DELETE RESTRICT</c>, que no se elimina un espacio con hijos o puntos relacionados.
/// </summary>
public sealed class SpacesDbContext(DbContextOptions<SpacesDbContext> options, ICurrentOrganization currentOrganization)
    : NexgateDbContext(options, currentOrganization)
{
    public const string Schema = "spaces";
    public const string RootIndex = "ux_space_site_root";
    public const string SiblingNameIndex = "ux_space_parent_name";
    public const string AccessPointNameIndex = "ux_access_point_site_name";

    public DbSet<Space> Spaces => Set<Space>();

    public DbSet<AccessPoint> AccessPoints => Set<AccessPoint>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasPostgresExtension("ltree");

        modelBuilder.Entity<Space>(space =>
        {
            space.ToTable("space", t =>
            {
                t.HasCheckConstraint("ck_space_root_is_site", "(parent_id IS NULL) = (type = 'site')");
                t.HasCheckConstraint("ck_space_depth", $"nlevel(path) <= {SpaceLimits.MaxDepth + 1}");
            });
            space.HasKey(s => s.Id);
            space.HasAlternateKey(s => new { s.SiteId, s.Id }).HasName("ak_space_site_id");
            space.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            space.IsTenantOwned();
            space.Property(s => s.SiteId).HasColumnName("site_id");
            space.Property(s => s.ParentId).HasColumnName("parent_id");
            space.Property(s => s.Path).HasColumnName("path").HasColumnType("ltree")
                .HasConversion(p => new LTree(p), p => p.ToString()).IsRequired();
            space.Property(s => s.Type).HasColumnName("type")
                .HasConversion(t => SpaceNames.ToName(t), t => ParseSpaceType(t))
                .HasMaxLength(30).IsRequired();
            space.Property(s => s.Name).HasColumnName("name").HasMaxLength(SpaceLimits.NameMaxLength).IsRequired();
            space.Property(s => s.IsCommon).HasColumnName("is_common");
            space.Property(s => s.CreatedAt).HasColumnName("created_at");
            space.Property(s => s.UpdatedAt).HasColumnName("updated_at");
            space.Ignore(s => s.IsRoot);
            space.Ignore(s => s.Depth);
            space.Ignore(s => s.PathIds);

            space.HasIndex(s => s.OrganizationId).HasDatabaseName("ix_space_organization_id");
            space.HasIndex(s => s.SiteId).IsUnique().HasFilter("parent_id IS NULL").HasDatabaseName(RootIndex);
            space.HasIndex(s => s.Path).HasMethod("gist").HasDatabaseName("ix_space_path");
            space.HasOne<Space>().WithMany()
                .HasForeignKey(s => new { s.SiteId, s.ParentId })
                .HasPrincipalKey(s => new { s.SiteId, s.Id })
                .HasConstraintName("fk_space_parent")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccessPoint>(point =>
        {
            point.ToTable("access_point", t =>
                t.HasCheckConstraint("ck_access_point_from_differs", "from_space_id IS NULL OR from_space_id <> to_space_id"));
            point.HasKey(p => p.Id);
            point.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
            point.IsTenantOwned();
            point.Property(p => p.SiteId).HasColumnName("site_id");
            point.Property(p => p.Name).HasColumnName("name").HasMaxLength(SpaceLimits.NameMaxLength).IsRequired();
            point.Property(p => p.Type).HasColumnName("type")
                .HasConversion(t => SpaceNames.ToName(t), t => ParseAccessPointType(t))
                .HasMaxLength(30).IsRequired();
            point.Property(p => p.ToSpaceId).HasColumnName("to_space_id");
            point.Property(p => p.FromSpaceId).HasColumnName("from_space_id");
            point.Property(p => p.FailMode).HasColumnName("fail_mode")
                .HasConversion(m => SpaceNames.ToName(m), m => ParseFailMode(m))
                .HasMaxLength(20).IsRequired();
            point.Property(p => p.CreatedAt).HasColumnName("created_at");
            point.Property(p => p.UpdatedAt).HasColumnName("updated_at");

            point.HasIndex(p => p.OrganizationId).HasDatabaseName("ix_access_point_organization_id");
            point.HasOne<Space>().WithMany()
                .HasForeignKey(p => new { p.SiteId, p.ToSpaceId })
                .HasPrincipalKey(s => new { s.SiteId, s.Id })
                .HasConstraintName("fk_access_point_to_space")
                .OnDelete(DeleteBehavior.Restrict);
            point.HasOne<Space>().WithMany()
                .HasForeignKey(p => new { p.SiteId, p.FromSpaceId })
                .HasPrincipalKey(s => new { s.SiteId, s.Id })
                .HasConstraintName("fk_access_point_from_space")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static SpaceType ParseSpaceType(string value) =>
        SpaceNames.TryParseSpaceType(value, out var type) ? type : throw new InvalidOperationException("Tipo de espacio desconocido en la base.");

    private static AccessPointType ParseAccessPointType(string value) =>
        SpaceNames.TryParseAccessPointType(value, out var type) ? type : throw new InvalidOperationException("Tipo de punto desconocido en la base.");

    private static FailMode ParseFailMode(string value) =>
        SpaceNames.TryParseFailMode(value, out var mode) ? mode : throw new InvalidOperationException("Modo de fallo desconocido en la base.");
}
