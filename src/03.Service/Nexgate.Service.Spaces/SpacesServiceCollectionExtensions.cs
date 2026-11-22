using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexgate.Data.Spaces;
using Nexgate.Service.Spaces.Contracts;
using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Service.Spaces;

/// <summary>Sitios de Tenancy para el puerto de la persistencia de Spaces (pasa por RLS).</summary>
internal sealed class SiteReferences(ISiteDirectory sites) : ISiteReferences
{
    public Task<bool> SiteExistsAsync(Guid siteId, CancellationToken cancellationToken) => sites.ExistsAsync(siteId, cancellationToken);
}

/// <summary>Espacios y puntos de acceso para otros módulos (pasa por RLS).</summary>
internal sealed class SpaceDirectory(ISpaceStore spaces, IAccessPointStore points, AccessPathQuery paths) : ISpaceDirectory
{
    public async Task<SpaceDto?> GetSpaceAsync(Guid spaceId, CancellationToken cancellationToken) =>
        await spaces.FindAsync(spaceId, cancellationToken) is { } space ? SpacesMapping.ToDto(space) : null;

    public async Task<AccessPointDto?> GetAccessPointAsync(Guid accessPointId, CancellationToken cancellationToken) =>
        await points.FindAsync(accessPointId, cancellationToken) is { } point ? SpacesMapping.ToDto(point) : null;

    public Task<IReadOnlyList<AccessPathEntryDto>?> GetAccessPathAsync(Guid spaceId, CancellationToken cancellationToken) =>
        paths.GetAsync(spaceId, cancellationToken);
}

/// <summary>Módulo Spaces: árbol de espacios y puntos de acceso (D-06). Requiere el módulo Tenancy registrado.</summary>
public static class SpacesServiceCollectionExtensions
{
    public static IServiceCollection AddSpacesService(this IServiceCollection services)
    {
        services.AddSpacesData();
        services.AddScoped<ISiteReferences, SiteReferences>();
        services.AddScoped<AccessPathQuery>();
        services.AddScoped<ISpaceService, SpaceService>();
        services.AddScoped<IAccessPointService, AccessPointService>();
        services.AddScoped<ISpaceDirectory, SpaceDirectory>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
