using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexgate.Data.Tenancy;
using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Service.Tenancy;

/// <summary>Organización activa para otros módulos (pasa por RLS).</summary>
internal sealed class OrganizationDirectory(IOrganizationStore store) : IOrganizationDirectory
{
    public async Task<OrganizationDto?> GetActiveOrganizationAsync(CancellationToken cancellationToken) =>
        await store.FindActiveAsync(cancellationToken) is { } organization ? OrganizationService.ToDto(organization) : null;
}

/// <summary>Sitios de la organización activa para otros módulos (pasa por RLS).</summary>
internal sealed class SiteDirectory(ISiteStore store) : ISiteDirectory
{
    public Task<bool> ExistsAsync(Guid siteId, CancellationToken cancellationToken) => store.ExistsAsync(siteId, cancellationToken);
}

/// <summary>Módulo Tenancy: organizaciones y sitios (D-02).</summary>
public static class TenancyServiceCollectionExtensions
{
    public static IServiceCollection AddTenancyService(this IServiceCollection services)
    {
        services.AddTenancyData();
        services.AddScoped<IOrganizationDirectory, OrganizationDirectory>();
        services.AddScoped<ISiteDirectory, SiteDirectory>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<ISiteService, SiteService>();
        services.TryAddSingleton<ITimeZoneCatalog, TimeZoneCatalog>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
