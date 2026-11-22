using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexgate.Common.BuildingBlocks.Persistence;
using Nexgate.Domain.Access;
using Npgsql;

namespace Nexgate.Data.Access;

/// <summary>Persistencia de horarios. Toda lectura y escritura está acotada a la organización activa.</summary>
public interface IScheduleStore
{
    Task<IReadOnlyList<Schedule>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Schedule>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<Schedule?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameInUseAsync(Guid siteId, string name, Guid? exceptId, CancellationToken cancellationToken);

    Task<bool> InUseAsync(Guid id, CancellationToken cancellationToken);

    void Add(Schedule schedule);

    void Remove(Schedule schedule);

    /// <summary>Guarda; false si el nombre ya está en uso o el horario quedó en uso (carrera con otra solicitud).</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Persistencia de credenciales. Toda lectura y escritura está acotada a la organización activa.</summary>
public interface ICredentialStore
{
    Task<IReadOnlyList<Credential>> ListBySiteAsync(Guid siteId, Guid? subjectId, CancellationToken cancellationToken);

    Task<Credential?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid siteId, CredentialType type, byte[] valueHash, CancellationToken cancellationToken);

    Task<bool> SubjectHasAnyAsync(Guid subjectId, CancellationToken cancellationToken);

    void Add(Credential credential);

    void Remove(Credential credential);

    /// <summary>Guarda; false si ya hay una credencial igual en el sitio (carrera con otra solicitud).</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Persistencia de otorgamientos. Toda lectura y escritura está acotada a la organización activa.</summary>
public interface IGrantStore
{
    Task<IReadOnlyList<Grant>> ListBySiteAsync(Guid siteId, Guid? subjectId, CancellationToken cancellationToken);

    /// <summary>Otorgamientos del sitio a alguno de los espacios indicados o al punto indicado.</summary>
    Task<IReadOnlyList<Grant>> ListCoveringAsync(Guid siteId, IReadOnlyCollection<Guid> spaceIds, Guid accessPointId,
        CancellationToken cancellationToken);

    Task<Grant?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> TargetInUseAsync(GrantTargetType type, Guid targetId, CancellationToken cancellationToken);

    Task<bool> SubjectHasAnyAsync(Guid subjectId, CancellationToken cancellationToken);

    void Add(Grant grant);

    void Remove(Grant grant);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

internal sealed class ScheduleStore(AccessDbContext db) : IScheduleStore
{
    public async Task<IReadOnlyList<Schedule>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken) =>
        (await db.Schedules.AsNoTracking().Where(s => s.SiteId == siteId).ToListAsync(cancellationToken))
        .OrderBy(s => s.Name, StringComparer.InvariantCultureIgnoreCase).ThenBy(s => s.Id).ToList();

    public async Task<IReadOnlyList<Schedule>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.ToList();
        return await db.Schedules.AsNoTracking().Where(s => list.Contains(s.Id)).ToListAsync(cancellationToken);
    }

    public Task<Schedule?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Schedules.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> NameInUseAsync(Guid siteId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var normalized = name.ToLower();
        return db.Schedules.AnyAsync(s => s.SiteId == siteId && s.Name.ToLower() == normalized && s.Id != exceptId, cancellationToken);
    }

    public Task<bool> InUseAsync(Guid id, CancellationToken cancellationToken) =>
        db.Grants.AnyAsync(g => g.ScheduleId == id, cancellationToken);

    public void Add(Schedule schedule) => db.Schedules.Add(schedule);

    public void Remove(Schedule schedule) => db.Schedules.Remove(schedule);

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
                                          {
                                              ConstraintName: AccessDbContext.ScheduleNameIndex or AccessDbContext.GrantScheduleForeignKey
                                          })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}

internal sealed class CredentialStore(AccessDbContext db) : ICredentialStore
{
    public async Task<IReadOnlyList<Credential>> ListBySiteAsync(Guid siteId, Guid? subjectId, CancellationToken cancellationToken)
    {
        var query = db.Credentials.AsNoTracking().Where(c => c.SiteId == siteId);
        if (subjectId is { } subject)
        {
            query = query.Where(c => c.SubjectId == subject);
        }

        return await query.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).ToListAsync(cancellationToken);
    }

    public Task<Credential?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Credentials.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid siteId, CredentialType type, byte[] valueHash, CancellationToken cancellationToken) =>
        db.Credentials.AnyAsync(c => c.SiteId == siteId && c.Type == type && c.ValueHash == valueHash, cancellationToken);

    public Task<bool> SubjectHasAnyAsync(Guid subjectId, CancellationToken cancellationToken) =>
        db.Credentials.AnyAsync(c => c.SubjectId == subjectId, cancellationToken);

    public void Add(Credential credential) => db.Credentials.Add(credential);

    public void Remove(Credential credential) => db.Credentials.Remove(credential);

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
                                              ConstraintName: AccessDbContext.CredentialValueIndex
                                          })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}

internal sealed class GrantStore(AccessDbContext db) : IGrantStore
{
    public async Task<IReadOnlyList<Grant>> ListBySiteAsync(Guid siteId, Guid? subjectId, CancellationToken cancellationToken)
    {
        var query = db.Grants.AsNoTracking().Where(g => g.SiteId == siteId);
        if (subjectId is { } subject)
        {
            query = query.Where(g => g.SubjectId == subject);
        }

        return await query.OrderBy(g => g.CreatedAt).ThenBy(g => g.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Grant>> ListCoveringAsync(Guid siteId, IReadOnlyCollection<Guid> spaceIds, Guid accessPointId,
        CancellationToken cancellationToken)
    {
        var spaces = spaceIds.ToList();
        return await db.Grants.AsNoTracking()
            .Where(g => g.SiteId == siteId
                        && ((g.TargetType == GrantTargetType.Space && spaces.Contains(g.TargetId))
                            || (g.TargetType == GrantTargetType.AccessPoint && g.TargetId == accessPointId)))
            .ToListAsync(cancellationToken);
    }

    public Task<Grant?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Grants.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);

    public Task<bool> TargetInUseAsync(GrantTargetType type, Guid targetId, CancellationToken cancellationToken) =>
        db.Grants.AnyAsync(g => g.TargetType == type && g.TargetId == targetId, cancellationToken);

    public Task<bool> SubjectHasAnyAsync(Guid subjectId, CancellationToken cancellationToken) =>
        db.Grants.AnyAsync(g => g.SubjectId == subjectId, cancellationToken);

    public void Add(Grant grant) => db.Grants.Add(grant);

    public void Remove(Grant grant) => db.Grants.Remove(grant);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

public static class AccessDataServiceCollectionExtensions
{
    /// <summary>Persistencia de Access (schema <c>access</c>). La registra <c>AddAccessService</c>.</summary>
    public static IServiceCollection AddAccessData(this IServiceCollection services)
    {
        services.AddModuleDbContext<AccessDbContext>(AccessDbContext.Schema);
        services.AddScoped<IScheduleStore, ScheduleStore>();
        services.AddScoped<ICredentialStore, CredentialStore>();
        services.AddScoped<IGrantStore, GrantStore>();
        return services;
    }
}
