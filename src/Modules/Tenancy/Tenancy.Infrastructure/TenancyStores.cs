using Microsoft.EntityFrameworkCore;
using Nexgate.BuildingBlocks.Persistence;
using Nexgate.Modules.Tenancy.Application;
using Nexgate.Modules.Tenancy.Domain;
using Npgsql;

namespace Nexgate.Modules.Tenancy.Infrastructure;

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
    public async Task<IReadOnlyList<Site>> ListAsync(CancellationToken cancellationToken) =>
        await db.Sites.AsNoTracking().OrderBy(s => s.Name).ThenBy(s => s.Id).ToListAsync(cancellationToken);

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
