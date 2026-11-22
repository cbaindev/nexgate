using Nexgate.Domain.Spaces;

namespace Nexgate.Data.Spaces;

/// <summary>
/// Sitios de otro módulo que Spaces necesita verificar. Lo implementa el servicio de Spaces con los contratos de
/// Tenancy (pasa por RLS: un sitio de otra organización no existe).
/// </summary>
public interface ISiteReferences
{
    Task<bool> SiteExistsAsync(Guid siteId, CancellationToken cancellationToken);
}

public enum SpaceSaveOutcome
{
    Saved,
    RootExists,
    NameConflict,
    HasDependents
}

/// <summary>Persistencia de espacios. Toda lectura y escritura está acotada a la organización activa.</summary>
public interface ISpaceStore
{
    Task<IReadOnlyList<Space>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken);

    Task<Space?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Space>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<bool> RootExistsAsync(Guid siteId, CancellationToken cancellationToken);

    /// <summary>Otro hijo del mismo padre ya usa el nombre (sin distinguir mayúsculas).</summary>
    Task<bool> SiblingNameInUseAsync(Guid parentId, string name, Guid? exceptSpaceId, CancellationToken cancellationToken);

    /// <summary>Tiene espacios hijos o puntos de acceso que dan acceso a él o se entran desde él.</summary>
    Task<bool> HasDependentsAsync(Guid spaceId, CancellationToken cancellationToken);

    void Add(Space space);

    void Remove(Space space);

    /// <summary>Guarda los cambios y traduce las violaciones de restricciones (carreras con otra solicitud).</summary>
    Task<SpaceSaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}

public enum AccessPointSaveOutcome
{
    Saved,
    NameConflict
}

/// <summary>Persistencia de puntos de acceso. Toda lectura y escritura está acotada a la organización activa.</summary>
public interface IAccessPointStore
{
    Task<IReadOnlyList<AccessPoint>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken);

    /// <summary>Puntos que dan acceso a alguno de los espacios indicados.</summary>
    Task<IReadOnlyList<AccessPoint>> ListByTargetsAsync(IReadOnlyCollection<Guid> spaceIds, CancellationToken cancellationToken);

    Task<AccessPoint?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameInUseAsync(Guid siteId, string name, Guid? exceptAccessPointId, CancellationToken cancellationToken);

    void Add(AccessPoint accessPoint);

    void Remove(AccessPoint accessPoint);

    Task<AccessPointSaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
