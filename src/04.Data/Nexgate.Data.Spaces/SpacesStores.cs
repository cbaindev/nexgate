using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexgate.Common.BuildingBlocks.Persistence;
using Nexgate.Domain.Spaces;
using Npgsql;

namespace Nexgate.Data.Spaces;

internal sealed class SpaceStore(SpacesDbContext db) : ISpaceStore
{
    public async Task<IReadOnlyList<Space>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken) =>
        await db.Spaces.AsNoTracking().Where(s => s.SiteId == siteId).ToListAsync(cancellationToken);

    public Task<Space?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Spaces.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Space>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.ToList();
        return await db.Spaces.AsNoTracking().Where(s => list.Contains(s.Id)).ToListAsync(cancellationToken);
    }

    public Task<bool> RootExistsAsync(Guid siteId, CancellationToken cancellationToken) =>
        db.Spaces.AnyAsync(s => s.SiteId == siteId && s.ParentId == null, cancellationToken);

    public Task<bool> SiblingNameInUseAsync(Guid parentId, string name, Guid? exceptSpaceId, CancellationToken cancellationToken)
    {
        var normalized = name.ToLower();
        return db.Spaces.AnyAsync(
            s => s.ParentId == parentId && s.Name.ToLower() == normalized && s.Id != exceptSpaceId, cancellationToken);
    }

    public async Task<bool> HasDependentsAsync(Guid spaceId, CancellationToken cancellationToken) =>
        await db.Spaces.AnyAsync(s => s.ParentId == spaceId, cancellationToken)
        || await db.AccessPoints.AnyAsync(p => p.ToSpaceId == spaceId || p.FromSpaceId == spaceId, cancellationToken);

    public void Add(Space space) => db.Spaces.Add(space);

    public void Remove(Space space) => db.Spaces.Remove(space);

    public async Task<SpaceSaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return SpaceSaveOutcome.Saved;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException postgres && Outcome(postgres) is { } outcome)
        {
            db.ChangeTracker.Clear();
            return outcome;
        }
    }

    private static SpaceSaveOutcome? Outcome(PostgresException e) => (e.SqlState, e.ConstraintName) switch
    {
        (PostgresErrorCodes.UniqueViolation, SpacesDbContext.RootIndex) => SpaceSaveOutcome.RootExists,
        (PostgresErrorCodes.UniqueViolation, SpacesDbContext.SiblingNameIndex) => SpaceSaveOutcome.NameConflict,
        (PostgresErrorCodes.ForeignKeyViolation, _) => SpaceSaveOutcome.HasDependents,
        _ => null
    };
}

internal sealed class AccessPointStore(SpacesDbContext db) : IAccessPointStore
{
    public async Task<IReadOnlyList<AccessPoint>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken) =>
        (await db.AccessPoints.AsNoTracking().Where(p => p.SiteId == siteId).ToListAsync(cancellationToken))
        .OrderBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase).ThenBy(p => p.Id).ToList();

    public async Task<IReadOnlyList<AccessPoint>> ListByTargetsAsync(IReadOnlyCollection<Guid> spaceIds, CancellationToken cancellationToken)
    {
        var ids = spaceIds.ToList();
        return await db.AccessPoints.AsNoTracking().Where(p => ids.Contains(p.ToSpaceId)).ToListAsync(cancellationToken);
    }

    public Task<AccessPoint?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.AccessPoints.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> NameInUseAsync(Guid siteId, string name, Guid? exceptAccessPointId, CancellationToken cancellationToken)
    {
        var normalized = name.ToLower();
        return db.AccessPoints.AnyAsync(
            p => p.SiteId == siteId && p.Name.ToLower() == normalized && p.Id != exceptAccessPointId, cancellationToken);
    }

    public void Add(AccessPoint accessPoint) => db.AccessPoints.Add(accessPoint);

    public void Remove(AccessPoint accessPoint) => db.AccessPoints.Remove(accessPoint);

    public async Task<AccessPointSaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return AccessPointSaveOutcome.Saved;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
                                          {
                                              SqlState: PostgresErrorCodes.UniqueViolation,
                                              ConstraintName: SpacesDbContext.AccessPointNameIndex
                                          })
        {
            db.ChangeTracker.Clear();
            return AccessPointSaveOutcome.NameConflict;
        }
    }
}

public static class SpacesDataServiceCollectionExtensions
{
    /// <summary>Persistencia de Spaces (schema <c>spaces</c>). La registra <c>AddSpacesService</c>.</summary>
    public static IServiceCollection AddSpacesData(this IServiceCollection services)
    {
        services.AddModuleDbContext<SpacesDbContext>(SpacesDbContext.Schema);
        services.AddScoped<ISpaceStore, SpaceStore>();
        services.AddScoped<IAccessPointStore, AccessPointStore>();
        return services;
    }
}
