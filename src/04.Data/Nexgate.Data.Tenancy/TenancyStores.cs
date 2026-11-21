using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexgate.Common.BuildingBlocks.Persistence;
using Nexgate.Domain.Tenancy;
using Npgsql;

namespace Nexgate.Data.Tenancy;

internal sealed class OrganizationStore(TenancyDbContext db) : IOrganizationStore
{
    public async Task AddAsPlatformOperationAsync(Organization organization, CancellationToken cancellationToken)
    {
        // La política de inserción de tenancy.organization exige esta marca, local a la transacción.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            $"SELECT set_config('{DatabaseRoles.PlatformOperationSetting}', 'on', true)", cancellationToken);
        db.Organizations.Add(organization);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<Organization?> FindActiveAsync(CancellationToken cancellationToken) =>
        db.Organizations.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
}

internal sealed class SiteStore(TenancyDbContext db) : ISiteStore
{
    public async Task<IReadOnlyList<Site>> ListAsync(IReadOnlyCollection<Guid>? onlySites, CancellationToken cancellationToken)
    {
        var query = db.Sites.AsNoTracking();
        if (onlySites is not null)
        {
            var ids = onlySites.ToList();
            query = query.Where(s => ids.Contains(s.Id));
        }

        return await query.OrderBy(s => s.Name).ThenBy(s => s.Id).ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Sites.AnyAsync(s => s.Id == id, cancellationToken);

    public Task<Site?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Sites.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> NameInUseAsync(string name, Guid? exceptSiteId, CancellationToken cancellationToken)
    {
        var normalized = name.ToLower();
        return db.Sites.AnyAsync(s => s.Name.ToLower() == normalized && s.Id != exceptSiteId, cancellationToken);
    }

    public void Add(Site site) => db.Sites.Add(site);

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
                                          {
                                              SqlState: PostgresErrorCodes.UniqueViolation,
                                              ConstraintName: TenancyDbContext.SiteNameIndex
                                          })
        {
            return false;
        }
    }
}

public static class TenancyDataServiceCollectionExtensions
{
    /// <summary>Persistencia de Tenancy (schema <c>tenancy</c>). La registra <c>AddTenancyService</c>.</summary>
    public static IServiceCollection AddTenancyData(this IServiceCollection services)
    {
        services.AddModuleDbContext<TenancyDbContext>(TenancyDbContext.Schema);
        services.AddScoped<IOrganizationStore, OrganizationStore>();
        services.AddScoped<ISiteStore, SiteStore>();
        return services;
    }
}
