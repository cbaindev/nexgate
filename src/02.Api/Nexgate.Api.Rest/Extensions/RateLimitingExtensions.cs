using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Nexgate.Service.Devices;
using Nexgate.Service.Identity;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Extensions;

public static class RateLimitingExtensions
{
    /// <summary>Política de inicio de sesión y aceptación de invitaciones (429 <c>too_many_requests</c>).</summary>
    public const string AuthPolicy = "identity-auth";

    /// <summary>Política del reclamo de dispositivos, con la cantidad de <c>Devices:ClaimPermitsPerMinute</c>.</summary>
    public const string DeviceClaimPolicy = "device-claim";

    /// <summary>Límite de intentos por IP (nunca por correo), con la cantidad configurada en <c>Identity:RateLimit</c>.</summary>
    public static IServiceCollection AddNexgateRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<IdentityModuleOptions>, IOptions<DevicesOptions>>((limiter, identity, devices) =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(AuthPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = identity.Value.RateLimit.PermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
            limiter.AddPolicy(DeviceClaimPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = devices.Value.ClaimPermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Demasiados intentos. Probá de nuevo en un minuto.",
                        Extensions = { ["code"] = IdentityErrorCodes.TooManyRequests }
                    }
                });
            };
        });
        return services;
    }
}
