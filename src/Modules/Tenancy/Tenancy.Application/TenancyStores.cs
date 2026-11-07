using Nexgate.Modules.Tenancy.Domain;

namespace Nexgate.Modules.Tenancy.Application;

/// <summary>Persistencia de organizaciones. Toda lectura está acotada a la organización activa.</summary>
public interface IOrganizationStore
{
    /// <summary>Alta de una organización como operación de plataforma (no hay organización activa).</summary>
    Task AddAsPlatformOperationAsync(Organization organization, CancellationToken cancellationToken);

    /// <summary>La organización activa, o null si no existe o no es visible.</summary>
    Task<Organization?> FindActiveAsync(CancellationToken cancellationToken);
}

/// <summary>Persistencia de sitios. Toda lectura y escritura está acotada a la organización activa.</summary>
public interface ISiteStore
{
    Task<IReadOnlyList<Site>> ListAsync(CancellationToken cancellationToken);

    Task<Site?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameInUseAsync(string name, Guid? exceptSiteId, CancellationToken cancellationToken);

    void Add(Site site);

    /// <summary>Guarda los cambios. Devuelve false si el nombre del sitio ya está en uso (carrera con otra solicitud).</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
