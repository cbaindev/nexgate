using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Data.Identity;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Spaces.Contracts;

namespace Nexgate.Service.Identity;

/// <summary>Módulo Identity: cuentas, inicio de sesión, membresías, invitaciones y roles por alcance (D-03, D-04, D-05).</summary>
public static class IdentityServiceCollectionExtensions
{
    /// <summary>
    /// Registra los servicios y la persistencia de Identity. La validación del token (JWT bearer) y el límite de intentos
    /// los configura la API con <see cref="TokenSigningKey"/> e <see cref="IdentityModuleOptions"/>.
    /// </summary>
    public static IServiceCollection AddIdentityService(this IServiceCollection services)
    {
        services.AddIdentityData();
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<IdentityModuleOptions>().BindConfiguration(IdentityModuleOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<IdentityModuleOptions>, IdentityModuleOptionsValidator>();
        services.AddOptions<PasswordHasherOptions>().Configure<IOptions<IdentityModuleOptions>>((hasher, identity) =>
        {
            hasher.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
            if (identity.Value.Passwords.IterationCount is > 0 and var iterations)
            {
                hasher.IterationCount = iterations;
            }
        });
        services.AddSingleton(sp =>
        {
            var tokens = sp.GetRequiredService<IOptions<IdentityModuleOptions>>().Value.Tokens;
            return new IdentityLifetimes(TimeSpan.FromDays(tokens.RefreshSessionDays), TimeSpan.FromDays(tokens.InvitationDays));
        });

        services.AddSingleton<TokenSigningKey>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IPasswordHashing, PasswordHashing>();

        services.AddScoped<IOrganizationNames, OrganizationNames>();
        services.AddScoped<IOrganizationReferences, OrganizationReferences>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<InvitationService>();
        services.AddScoped<IInvitationService>(sp => sp.GetRequiredService<InvitationService>());
        services.AddScoped<IAdministratorInvitations>(sp => sp.GetRequiredService<InvitationService>());
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPlatformOperators, PlatformOperatorService>();
        services.AddScoped<IRequestAccessResolver, IdentityAccessResolver>();
        services.AddScoped<IPlatformAccess, IdentityPlatformAccess>();
        services.AddScoped<ScopeReferences>();
        services.AddScoped<ISpaceUsage, IdentitySpaceUsage>();
        return services;
    }
}
