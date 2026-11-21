using Nexgate.Common.BuildingBlocks.Persistence;
using Nexgate.Service.Identity;
using Nexgate.Service.Sample;
using Nexgate.Service.Tenancy;

namespace Nexgate.Api.Rest.Extensions;

/// <summary>
/// Raíz de composición de los módulos: registra los servicios y la persistencia de cada uno, y la autenticación y el
/// límite de intentos que dependen de Identity.
/// Los controllers solo usan los contratos públicos; esta es la única parte de la API que conoce las capas internas.
/// </summary>
public static class ModuleExtensions
{
    public static IServiceCollection AddModules(this IServiceCollection services)
    {
        services.AddDatabaseMigrator();
        services.AddSampleService();
        services.AddTenancyService();
        services.AddIdentityService();
        services.AddNexgateAuthentication();
        services.AddNexgateRateLimiting();
        return services;
    }
}
