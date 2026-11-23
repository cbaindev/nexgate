using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Nexgate.Data.Access;
using Nexgate.Domain.Access;
using Nexgate.Service.Access.Contracts;
using Nexgate.Service.Occupancy.Contracts;
using Nexgate.Service.Spaces.Contracts;

namespace Nexgate.Service.Access;

/// <summary>Un espacio destino de un otorgamiento no se elimina.</summary>
internal sealed class GrantSpaceUsage(IGrantStore grants) : ISpaceUsage
{
    public Task<bool> IsUsedAsync(Guid spaceId, CancellationToken cancellationToken) =>
        grants.TargetInUseAsync(GrantTargetType.Space, spaceId, cancellationToken);
}

/// <summary>Un punto de acceso destino de un otorgamiento no se elimina.</summary>
internal sealed class GrantAccessPointUsage(IGrantStore grants) : IAccessPointUsage
{
    public Task<bool> IsUsedAsync(Guid accessPointId, CancellationToken cancellationToken) =>
        grants.TargetInUseAsync(GrantTargetType.AccessPoint, accessPointId, cancellationToken);
}

/// <summary>Un sujeto con credenciales u otorgamientos no se elimina.</summary>
internal sealed class AccessSubjectUsage(ICredentialStore credentials, IGrantStore grants) : ISubjectUsage
{
    public async Task<bool> IsUsedAsync(Guid subjectId, CancellationToken cancellationToken) =>
        await credentials.SubjectHasAnyAsync(subjectId, cancellationToken) || await grants.SubjectHasAnyAsync(subjectId, cancellationToken);
}

/// <summary>
/// Módulo Access: horarios, credenciales, otorgamientos, política compilada y decisión de acceso (D-13). Requiere los
/// módulos Tenancy, Spaces y Occupancy registrados.
/// </summary>
public static class AccessServiceCollectionExtensions
{
    public static IServiceCollection AddAccessService(this IServiceCollection services)
    {
        services.AddAccessData();
        services.AddOptions<AccessOptions>().BindConfiguration(AccessOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<AccessOptions>, AccessOptionsValidator>();
        services.AddSingleton<CredentialKey>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<ICredentialService, CredentialService>();
        services.AddScoped<IGrantService, GrantService>();
        services.AddScoped<IAccessPolicyService, AccessPolicyService>();
        services.AddSingleton<ISiteCredentialKeys, SiteCredentialKeys>();
        services.AddScoped<ICredentialLookup, CredentialLookup>();
        services.AddScoped<IGrantUses, GrantUses>();
        services.AddScoped<ISpaceUsage, GrantSpaceUsage>();
        services.AddScoped<IAccessPointUsage, GrantAccessPointUsage>();
        services.AddScoped<ISubjectUsage, AccessSubjectUsage>();
        return services;
    }
}
