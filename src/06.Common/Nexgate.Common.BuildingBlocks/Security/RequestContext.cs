using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexgate.Common.BuildingBlocks.Tenancy;

namespace Nexgate.Common.BuildingBlocks.Security;

/// <summary>Usuario autenticado de la solicitud o del alcance actual, si lo hay.</summary>
public interface ICurrentUser
{
    Guid? Id { get; }
}

/// <summary>
/// Un permiso efectivo en la organización activa, con su alcance: <see cref="SiteId"/> nulo es alcance de organización
/// (cubre todos los sitios); con <see cref="SiteId"/> y sin <see cref="SpaceId"/>, el sitio entero; con
/// <see cref="SpaceId"/>, ese espacio y sus descendientes (<see cref="SiteId"/> es el sitio que lo contiene).
/// </summary>
public sealed record PermissionGrant(string Permission, Guid? SiteId, Guid? SpaceId = null);

/// <summary>
/// Contexto de seguridad de un alcance de DI (una solicitud HTTP o un alcance creado con
/// <see cref="IContextScopeFactory"/>): usuario, organización activa y permisos efectivos. En una solicitud lo completa
/// <see cref="RequestContextMiddleware"/> a partir del token validado, nunca a partir del contenido de la solicitud.
/// </summary>
public sealed class RequestContext : ICurrentOrganization, ICurrentUser
{
    public Guid? UserId { get; private set; }

    public Guid? OrganizationId { get; private set; }

    /// <summary>Permisos efectivos en la organización activa. Vacío sin organización activa.</summary>
    public IReadOnlyList<PermissionGrant> Grants { get; private set; } = [];

    Guid? ICurrentOrganization.Id => OrganizationId;

    Guid? ICurrentUser.Id => UserId;

    public void SetUser(Guid? userId) => UserId = userId;

    public void SetOrganization(Guid? organizationId, IReadOnlyList<PermissionGrant>? grants = null)
    {
        OrganizationId = organizationId;
        Grants = organizationId is null ? [] : grants ?? [];
    }
}

/// <summary>Un espacio del alcance de un permiso, con el sitio que lo contiene.</summary>
public sealed record ScopedSpace(Guid SpaceId, Guid SiteId);

/// <summary>
/// Dónde aplica un permiso: toda la organización, sitios enteros y subárboles de espacios. Los alcances de un mismo
/// usuario se suman.
/// </summary>
public sealed record PermissionScope(bool Organization, IReadOnlySet<Guid> Sites, IReadOnlyList<ScopedSpace> Spaces)
{
    public static readonly PermissionScope None = new(false, new HashSet<Guid>(), []);

    public bool Any => Organization || Sites.Count > 0 || Spaces.Count > 0;

    /// <summary>Aplica al sitio entero (alcance de organización o del sitio).</summary>
    public bool CoversSite(Guid siteId) => Organization || Sites.Contains(siteId);

    /// <summary>Aplica al sitio entero o a algún espacio del sitio.</summary>
    public bool ReachesSite(Guid siteId) => CoversSite(siteId) || Spaces.Any(s => s.SiteId == siteId);

    /// <summary>
    /// Aplica a un recurso del árbol: cubre su sitio entero o alguno de los espacios de su camino
    /// (<paramref name="pathIds"/>: desde la raíz hasta el recurso inclusive) es un espacio del alcance.
    /// </summary>
    public bool Covers(Guid siteId, IEnumerable<Guid> pathIds) =>
        CoversSite(siteId) || (Spaces.Count > 0 && pathIds.Any(id => Spaces.Any(s => s.SpaceId == id && s.SiteId == siteId)));
}

/// <summary>Sitios enteros sobre los que aplica un permiso: todos (alcance de organización) o una lista.</summary>
public sealed record SiteAccess(bool AllSites, IReadOnlySet<Guid> SiteIds)
{
    public static readonly SiteAccess None = new(false, new HashSet<Guid>());

    public static readonly SiteAccess All = new(true, new HashSet<Guid>());

    public bool Any => AllSites || SiteIds.Count > 0;

    public bool Allows(Guid siteId) => AllSites || SiteIds.Contains(siteId);
}

/// <summary>Permisos efectivos del llamador en la organización activa. Se evalúan en el servidor en cada solicitud.</summary>
public interface ICurrentPermissions
{
    /// <summary>Tiene el permiso con cualquier alcance.</summary>
    bool Has(string permission);

    /// <summary>Tiene el permiso con alcance de organización.</summary>
    bool HasAtOrganization(string permission);

    /// <summary>Sitios enteros donde aplica el permiso (sin los alcances de espacio).</summary>
    SiteAccess SitesFor(string permission);

    /// <summary>Alcance completo del permiso: organización, sitios enteros y espacios.</summary>
    PermissionScope ScopeFor(string permission);
}

public sealed class CurrentPermissions(RequestContext context) : ICurrentPermissions
{
    public bool Has(string permission) => context.Grants.Any(g => g.Permission == permission);

    public bool HasAtOrganization(string permission) =>
        context.Grants.Any(g => g.Permission == permission && g.SiteId is null);

    public SiteAccess SitesFor(string permission)
    {
        if (HasAtOrganization(permission))
        {
            return SiteAccess.All;
        }

        var sites = context.Grants
            .Where(g => g.Permission == permission && g.SiteId is not null && g.SpaceId is null)
            .Select(g => g.SiteId!.Value)
            .ToHashSet();
        return sites.Count == 0 ? SiteAccess.None : new SiteAccess(false, sites);
    }

    public PermissionScope ScopeFor(string permission)
    {
        var grants = context.Grants.Where(g => g.Permission == permission).ToList();
        if (grants.Count == 0)
        {
            return PermissionScope.None;
        }

        return new PermissionScope(
            grants.Any(g => g.SiteId is null),
            grants.Where(g => g.SiteId is not null && g.SpaceId is null).Select(g => g.SiteId!.Value).ToHashSet(),
            grants.Where(g => g.SpaceId is not null).Select(g => new ScopedSpace(g.SpaceId!.Value, g.SiteId!.Value)).Distinct().ToList());
    }
}

/// <summary>
/// Crea alcances de DI con un usuario y una organización fijos. Lo usan los flujos que actúan en nombre de alguien
/// distinto del llamador (inicio de sesión, aceptación de invitaciones, nombres del selector de organización):
/// el acceso a datos de ese alcance fija esos valores en la sesión de PostgreSQL y RLS hace el resto.
/// Quien lo use es responsable de haber verificado antes que el usuario tiene derecho a actuar así.
/// </summary>
public interface IContextScopeFactory
{
    AsyncServiceScope Create(Guid? userId, Guid? organizationId);
}

internal sealed class ContextScopeFactory(IServiceScopeFactory scopes) : IContextScopeFactory
{
    public AsyncServiceScope Create(Guid? userId, Guid? organizationId)
    {
        var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RequestContext>();
        context.SetUser(userId);
        context.SetOrganization(organizationId);
        return scope;
    }
}

public static class RequestContextRegistration
{
    /// <summary>Registra el contexto de seguridad (idempotente): lo usan la API, el acceso a datos y los workers.</summary>
    public static IServiceCollection AddNexgateRequestContext(this IServiceCollection services)
    {
        services.TryAddScoped<RequestContext>();
        services.TryAddScoped<ICurrentOrganization>(sp => sp.GetRequiredService<RequestContext>());
        services.TryAddScoped<ICurrentUser>(sp => sp.GetRequiredService<RequestContext>());
        services.TryAddScoped<ICurrentPermissions, CurrentPermissions>();
        services.TryAddSingleton<IContextScopeFactory, ContextScopeFactory>();
        return services;
    }
}
