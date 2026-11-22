using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Domain.Identity;

/// <summary>Conversión entre los roles y alcances del dominio y sus valores públicos.</summary>
public static class IdentityNames
{
    public static bool TryParseRole(string? value, out Role role)
    {
        (var ok, role) = value switch
        {
            RoleNames.Administrator => (true, Role.Administrator),
            RoleNames.Guard => (true, Role.Guard),
            RoleNames.Resident => (true, Role.Resident),
            RoleNames.Employee => (true, Role.Employee),
            _ => (false, default)
        };
        return ok;
    }

    public static string ToName(Role role) => role switch
    {
        Role.Administrator => RoleNames.Administrator,
        Role.Guard => RoleNames.Guard,
        Role.Resident => RoleNames.Resident,
        Role.Employee => RoleNames.Employee,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public static bool TryParseScope(string? value, out ScopeType scope)
    {
        (var ok, scope) = value switch
        {
            ScopeTypeNames.Organization => (true, ScopeType.Organization),
            ScopeTypeNames.Site => (true, ScopeType.Site),
            ScopeTypeNames.Space => (true, ScopeType.Space),
            _ => (false, default)
        };
        return ok;
    }

    public static string ToName(ScopeType scope) => scope switch
    {
        ScopeType.Organization => ScopeTypeNames.Organization,
        ScopeType.Site => ScopeTypeNames.Site,
        ScopeType.Space => ScopeTypeNames.Space,
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };
}

/// <summary>
/// Catálogo de permisos por rol (design, decisión 7). Los permisos se derivan solo de las asignaciones vigentes de la
/// organización activa; con alcance de sitio cubren solo ese sitio.
/// </summary>
public static class PermissionCatalog
{
    private static readonly IReadOnlyDictionary<Role, string[]> ByRole = new Dictionary<Role, string[]>
    {
        [Role.Administrator] =
        [
            Permissions.OrganizationRead, Permissions.SitesRead, Permissions.SitesManage,
            Permissions.MembersRead, Permissions.MembersManage, Permissions.SpacesRead, Permissions.SpacesManage,
            Permissions.AccessRead, Permissions.AccessManage
        ],
        [Role.Guard] = [Permissions.OrganizationRead, Permissions.SitesRead, Permissions.SpacesRead, Permissions.AccessRead],
        [Role.Resident] = [Permissions.OrganizationRead, Permissions.SitesRead],
        [Role.Employee] = [Permissions.OrganizationRead, Permissions.SitesRead]
    };

    public static IReadOnlyList<string> PermissionsOf(Role role) => ByRole[role];

    /// <summary>
    /// Permisos efectivos sin repetir: sitio nulo es alcance de organización; con espacio, el sitio es el que lo contiene
    /// (<paramref name="siteOfSpace"/>). Un espacio sin sitio conocido (ya no existe) no otorga nada.
    /// </summary>
    public static IReadOnlyList<(string Permission, Guid? SiteId, Guid? SpaceId)> Grants(
        IEnumerable<RoleAssignment> assignments, DateTimeOffset now, IReadOnlyDictionary<Guid, Guid>? siteOfSpace = null) =>
        assignments
            .Where(a => a.IsValidAt(now))
            .SelectMany(a => Scope(a, siteOfSpace) is { } scope
                ? ByRole[a.Role].Select(p => (Permission: p, scope.SiteId, scope.SpaceId))
                : [])
            .Distinct()
            .ToList();

    private static (Guid? SiteId, Guid? SpaceId)? Scope(RoleAssignment assignment, IReadOnlyDictionary<Guid, Guid>? siteOfSpace) =>
        assignment.ScopeType switch
        {
            ScopeType.Organization => (null, null),
            ScopeType.Site => (assignment.ScopeId, null),
            ScopeType.Space when assignment.ScopeId is { } space && siteOfSpace is not null
                                 && siteOfSpace.TryGetValue(space, out var site) => (site, space),
            _ => null
        };
}
