using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.IdentityModel.Tokens;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Devices;
using Nexgate.Service.Devices.Contracts;
using Nexgate.Service.Identity;

namespace Nexgate.Api.Rest.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Valida el token de acceso que emite Identity (ES256, emisor y audiencia propios). Los claims no se renombran:
    /// el contexto de la solicitud lee <c>sub</c> y <c>org_id</c> tal cual. Suma el esquema de certificado de cliente de
    /// los dispositivos, que no es el esquema por defecto.
    /// </summary>
    public static IServiceCollection AddNexgateAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer()
            .AddCertificate(DeviceAuthentication.Scheme);
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
        services.AddOptions<CertificateAuthenticationOptions>(DeviceAuthentication.Scheme).Configure<DeviceCertificateAuthority>(
            (certificate, authority) =>
            {
                // Única raíz: la autoridad de dispositivos de Nexgate. Sin revisión de revocación: el backend compara la
                // huella con la vigente del dispositivo en cada solicitud (design, decisión 2).
                certificate.AllowedCertificateTypes = CertificateTypes.Chained;
                certificate.ChainTrustValidationMode = X509ChainTrustMode.CustomRootTrust;
                certificate.CustomTrustStore = [authority.PublicCertificate];
                certificate.RevocationMode = X509RevocationMode.NoCheck;
                certificate.ValidateCertificateUse = true;
                certificate.ValidateValidityPeriod = true;
                certificate.Events = new CertificateAuthenticationEvents
                {
                    OnCertificateValidated = async context =>
                    {
                        var authenticator = context.HttpContext.RequestServices.GetRequiredService<IDeviceAuthenticator>();
                        var device = await authenticator.AuthenticateAsync(context.ClientCertificate.RawData, context.HttpContext.RequestAborted);
                        if (device is null)
                        {
                            context.Fail("El certificado no es el vigente de un dispositivo activo.");
                            return;
                        }

                        context.Principal = DeviceAuthentication.Principal(device);
                        context.Success();
                    }
                };
            });
        return services;
    }

    /// <summary>
    /// Kestrel acepta cualquier certificado de cliente a nivel TLS en los endpoints que lo piden (el de dispositivos,
    /// <c>Kestrel:Endpoints:Devices</c> con <c>ClientCertificateMode: AllowCertificate</c>): lo valida el esquema de
    /// autenticación contra la autoridad de dispositivos, no el almacén del sistema operativo.
    /// </summary>
    public static WebApplicationBuilder AddDeviceEndpoint(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ConfigureHttpsDefaults(https => https.AllowAnyClientCertificate()));
        return builder;
    }
}
