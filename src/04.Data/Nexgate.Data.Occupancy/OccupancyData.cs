using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexgate.Common.BuildingBlocks.Persistence;
using Nexgate.Common.BuildingBlocks.Tenancy;
using Nexgate.Domain.Occupancy;
using Nexgate.Service.Occupancy.Contracts;

namespace Nexgate.Data.Occupancy;

/// <summary>Schema <c>occupancy</c>. Por ahora solo sujetos (design de add-access-evaluator, decisión 1).</summary>
public sealed class OccupancyDbContext(DbContextOptions<OccupancyDbContext> options, ICurrentOrganization currentOrganization)
    : NexgateDbContext(options, currentOrganization)
{
    public const string Schema = "occupancy";

    public DbSet<Subject> Subjects => Set<Subject>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Subject>(subject =>
        {
            subject.ToTable("subject");
            subject.HasKey(s => s.Id);
            subject.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            subject.IsTenantOwned();
            subject.Property(s => s.SiteId).HasColumnName("site_id");
            subject.Property(s => s.Kind).HasColumnName("kind")
                .HasConversion(k => SubjectKinds.ToName(k), k => ParseKind(k))
                .HasMaxLength(20).IsRequired();
            subject.Property(s => s.Alias).HasColumnName("alias").HasMaxLength(SubjectLimits.AliasMaxLength).IsRequired();
            subject.Property(s => s.CreatedBy).HasColumnName("created_by");
            subject.Property(s => s.CreatedAt).HasColumnName("created_at");
            subject.Property(s => s.UpdatedAt).HasColumnName("updated_at");
            subject.HasIndex(s => s.OrganizationId).HasDatabaseName("ix_subject_organization_id");
            subject.HasIndex(s => s.SiteId).HasDatabaseName("ix_subject_site_id");
        });
    }

    private static SubjectKind ParseKind(string value) =>
        SubjectKinds.TryParse(value, out var kind) ? kind : throw new InvalidOperationException("Tipo de sujeto desconocido en la base.");
}

/// <summary>Sitios de Tenancy que Occupancy verifica. Lo implementa el servicio con los contratos de Tenancy (RLS).</summary>
public interface ISiteReferences
{
    Task<bool> SiteExistsAsync(Guid siteId, CancellationToken cancellationToken);
}

/// <summary>Persistencia de sujetos. Toda lectura y escritura está acotada a la organización activa.</summary>
public interface ISubjectStore
{
    Task<IReadOnlyList<Subject>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken);

    Task<Subject?> FindAsync(Guid id, CancellationToken cancellationToken);

    void Add(Subject subject);

    void Remove(Subject subject);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

internal sealed class SubjectStore(OccupancyDbContext db) : ISubjectStore
{
    public async Task<IReadOnlyList<Subject>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken) =>
        (await db.Subjects.AsNoTracking().Where(s => s.SiteId == siteId).ToListAsync(cancellationToken))
        .OrderBy(s => s.Alias, StringComparer.InvariantCultureIgnoreCase).ThenBy(s => s.Id).ToList();

    public Task<Subject?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Subjects.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public void Add(Subject subject) => db.Subjects.Add(subject);

    public void Remove(Subject subject) => db.Subjects.Remove(subject);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

public static class OccupancyDataServiceCollectionExtensions
{
    /// <summary>Persistencia de Occupancy (schema <c>occupancy</c>). La registra <c>AddOccupancyService</c>.</summary>
    public static IServiceCollection AddOccupancyData(this IServiceCollection services)
    {
        services.AddModuleDbContext<OccupancyDbContext>(OccupancyDbContext.Schema);
        services.AddScoped<ISubjectStore, SubjectStore>();
        return services;
    }
}
