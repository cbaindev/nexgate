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
            _ => (false, default)
        };
        return ok;
    }

    public static string ToName(ScopeType scope) => scope switch
    {
        ScopeType.Organization => ScopeTypeNames.Organization,
        ScopeType.Site => ScopeTypeNames.Site,
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
            Permissions.MembersRead, Permissions.MembersManage, Permissions.SpacesRead, Permissions.SpacesManage
        ],
        [Role.Guard] = [Permissions.OrganizationRead, Permissions.SitesRead, Permissions.SpacesRead],
        [Role.Resident] = [Permissions.OrganizationRead, Permissions.SitesRead],
        [Role.Employee] = [Permissions.OrganizationRead, Permissions.SitesRead]
    };

    public static IReadOnlyList<string> PermissionsOf(Role role) => ByRole[role];

    /// <summary>Pares (permiso, sitio) sin repetir; sitio nulo es alcance de organización.</summary>
    public static IReadOnlyList<(string Permission, Guid? SiteId)> Grants(IEnumerable<RoleAssignment> assignments, DateTimeOffset now) =>
        assignments
            .Where(a => a.IsValidAt(now))
            .SelectMany(a => ByRole[a.Role].Select(p => (Permission: p, SiteId: a.ScopeType == ScopeType.Organization ? null : a.ScopeId)))
            .Distinct()
            .ToList();
}
