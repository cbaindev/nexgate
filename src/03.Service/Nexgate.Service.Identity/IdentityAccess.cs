using Microsoft.Extensions.DependencyInjection;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Data.Identity;
using Nexgate.Domain.Identity;
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

/// <summary>Membresía y permisos efectivos de la solicitud, en una sola consulta bajo RLS (design, decisión 7).</summary>
internal sealed class IdentityAccessResolver(IAccessQueries queries, TimeProvider clock) : IRequestAccessResolver
{
    public async Task<IReadOnlyList<PermissionGrant>?> ResolveAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken) =>
        await queries.FindMembershipAssignmentsAsync(userId, organizationId, cancellationToken) is { } assignments
            ? PermissionCatalog.Grants(assignments, clock.GetUtcNow())
                .Select(g => new PermissionGrant(g.Permission, g.SiteId))
                .ToList()
            : null;
}

/// <summary>El operador de plataforma se lee de la base en cada solicitud, no del token.</summary>
internal sealed class IdentityPlatformAccess(IAccessQueries queries, ICurrentUser user) : IPlatformAccess
{
    public async Task<bool> IsPlatformOperatorAsync(CancellationToken cancellationToken) =>
        user.Id is { } userId && await queries.IsPlatformOperatorAsync(userId, cancellationToken);
}

/// <summary>Referencias a Tenancy para la persistencia de Identity, por sus contratos públicos.</summary>
internal sealed class OrganizationReferences(IOrganizationDirectory organizations, ISiteDirectory sites) : IOrganizationReferences
{
    public async Task<bool> ActiveOrganizationExistsAsync(CancellationToken cancellationToken) =>
        await organizations.GetActiveOrganizationAsync(cancellationToken) is not null;

    public Task<bool> SiteExistsAsync(Guid siteId, CancellationToken cancellationToken) => sites.ExistsAsync(siteId, cancellationToken);
}
