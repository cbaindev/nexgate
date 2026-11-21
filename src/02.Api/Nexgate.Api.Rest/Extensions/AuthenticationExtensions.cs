using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Identity;

namespace Nexgate.Api.Rest.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Valida el token de acceso que emite Identity (ES256, emisor y audiencia propios). Los claims no se renombran:
    /// el contexto de la solicitud lee <c>sub</c> y <c>org_id</c> tal cual.
    /// </summary>
    public static IServiceCollection AddNexgateAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<TokenSigningKey>((jwt, key) =>
        {
            jwt.MapInboundClaims = false;
            jwt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = TokenSigningKey.Issuer,
                ValidAudience = TokenSigningKey.Audience,
                IssuerSigningKey = key.Key,
                ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = AccessClaims.Subject
            };
        });
        return services;
    }
}
