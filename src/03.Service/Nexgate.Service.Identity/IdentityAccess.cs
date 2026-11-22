using Microsoft.Extensions.DependencyInjection;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Data.Identity;
using Nexgate.Domain.Identity;
using Nexgate.Service.Spaces.Contracts;
using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Service.Identity;

/// <summary>Lee el nombre de cada organización con esa organización activa (design, decisión 8).</summary>
internal sealed class OrganizationNames(IContextScopeFactory scopes) : IOrganizationNames
{
    public async Task<string?> GetNameAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.Create(userId, organizationId);
        var organization = await scope.ServiceProvider.GetRequiredService<IOrganizationDirectory>().GetActiveOrganizationAsync(cancellationToken);
        return organization?.Name;
    }
}

/// <summary>
/// Membresía y permisos efectivos de la solicitud bajo RLS (design de add-identity-and-roles, decisión 7). Con alcances de
/// espacio, el sitio de cada espacio se pide a Spaces en una sola consulta (design de add-space-scoped-roles, decisión 2).
/// </summary>
internal sealed class IdentityAccessResolver(IAccessQueries queries, ISpaceDirectory spaces, TimeProvider clock) : IRequestAccessResolver
{
    public async Task<IReadOnlyList<PermissionGrant>?> ResolveAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        if (await queries.FindMembershipAssignmentsAsync(userId, organizationId, cancellationToken) is not { } assignments)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        var spaceIds = assignments
            .Where(a => a.ScopeType == ScopeType.Space && a.ScopeId is not null && a.IsValidAt(now))
            .Select(a => a.ScopeId!.Value)
            .Distinct()
            .ToList();
        var siteOfSpace = spaceIds.Count == 0
            ? null
            : (await spaces.GetSpacesAsync(spaceIds, cancellationToken)).ToDictionary(s => s.Id, s => s.SiteId);

        return PermissionCatalog.Grants(assignments, now, siteOfSpace)
            .Select(g => new PermissionGrant(g.Permission, g.SiteId, g.SpaceId))
            .ToList();
    }
}

/// <summary>Un espacio usado como alcance de un rol o de una invitación pendiente no se puede eliminar.</summary>
internal sealed class IdentitySpaceUsage(IAccessQueries queries, TimeProvider clock) : ISpaceUsage
{
    public Task<bool> IsUsedAsync(Guid spaceId, CancellationToken cancellationToken) =>
        queries.IsSpaceScopeInUseAsync(spaceId, clock.GetUtcNow(), cancellationToken);
}

/// <summary>El operador de plataforma se lee de la base en cada solicitud, no del token.</summary>
internal sealed class IdentityPlatformAccess(IAccessQueries queries, ICurrentUser user) : IPlatformAccess
{
    public async Task<bool> IsPlatformOperatorAsync(CancellationToken cancellationToken) =>
        user.Id is { } userId && await queries.IsPlatformOperatorAsync(userId, cancellationToken);
}

/// <summary>Referencias a Tenancy y Spaces para la persistencia de Identity, por sus contratos públicos.</summary>
internal sealed class OrganizationReferences(IOrganizationDirectory organizations, ISiteDirectory sites, ISpaceDirectory spaces)
    : IOrganizationReferences
{
    public async Task<bool> ActiveOrganizationExistsAsync(CancellationToken cancellationToken) =>
        await organizations.GetActiveOrganizationAsync(cancellationToken) is not null;

    public Task<bool> SiteExistsAsync(Guid siteId, CancellationToken cancellationToken) => sites.ExistsAsync(siteId, cancellationToken);

    public async Task<bool> SpaceExistsAsync(Guid spaceId, CancellationToken cancellationToken) =>
        await spaces.GetSpaceAsync(spaceId, cancellationToken) is not null;
}

/// <summary>Sitios y espacios de la organización activa para validar alcances (los directorios pasan por RLS).</summary>
internal sealed class ScopeReferences(ISiteDirectory sites, ISpaceDirectory spaces)
{
    public Task<bool> SiteExistsAsync(Guid siteId, CancellationToken cancellationToken) => sites.ExistsAsync(siteId, cancellationToken);

    public async Task<bool> SpaceExistsAsync(Guid spaceId, CancellationToken cancellationToken) =>
        await spaces.GetSpaceAsync(spaceId, cancellationToken) is not null;
}
