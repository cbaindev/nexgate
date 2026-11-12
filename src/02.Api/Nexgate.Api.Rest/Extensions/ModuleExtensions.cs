using Nexgate.Service.Sample;

namespace Nexgate.Api.Rest.Extensions;

/// <summary>
/// Raíz de composición de los módulos: registra los servicios (y, cuando existan, la persistencia) de cada uno.
/// Los controllers solo usan los contratos públicos; esta es la única parte de la API que conoce las capas internas.
/// </summary>
public static class ModuleExtensions
{
    public static IServiceCollection AddModules(this IServiceCollection services)
    {
        services.AddSampleService();
        return services;
    }
}
