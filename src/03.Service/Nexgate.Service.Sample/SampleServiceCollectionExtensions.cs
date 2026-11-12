using Microsoft.Extensions.DependencyInjection;
using Nexgate.Service.Sample.Contracts;

namespace Nexgate.Service.Sample;

/// <summary>Registro DI del módulo de ejemplo. Lo invoca la raíz de composición (Nexgate.Api.Rest).</summary>
public static class SampleServiceCollectionExtensions
{
    public static IServiceCollection AddSampleService(this IServiceCollection services)
    {
        services.AddSingleton<IGreetingService, GreetingService>();
        return services;
    }
}
