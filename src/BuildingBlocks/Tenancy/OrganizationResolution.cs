using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Nexgate.BuildingBlocks.Tenancy;

public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    public DevelopmentAccessOptions DevelopmentAccess { get; set; } = new();

    /// <summary>
    /// Mecanismo de desarrollo (hasta que exista identidad): acepta la organización activa en el encabezado
    /// <see cref="OrganizationResolutionMiddleware.DevelopmentHeader"/> y habilita los endpoints de plataforma.
    /// Apagado por defecto; la aplicación no arranca si se habilita fuera de Development.
    /// </summary>
    public sealed class DevelopmentAccessOptions
    {
        public bool Enabled { get; set; }
    }
}

/// <summary>Rechaza el arranque si el mecanismo de desarrollo está habilitado fuera del entorno Development.</summary>
public sealed class TenancyOptionsValidator(IHostEnvironment environment) : IValidateOptions<TenancyOptions>
{
    public ValidateOptionsResult Validate(string? name, TenancyOptions options) =>
        options.DevelopmentAccess.Enabled && !environment.IsDevelopment()
            ? ValidateOptionsResult.Fail(
                "Tenancy:DevelopmentAccess:Enabled solo puede habilitarse en el entorno Development.")
            : ValidateOptionsResult.Success;
}

/// <summary>
/// Resuelve la organización activa de la solicitud y la publica en <c>HttpContext.Items</c>, de donde la leen
/// <see cref="ICurrentOrganization"/>, la telemetría y el acceso a datos.
/// Fuente confiable: el claim <see cref="OrganizationClaim"/> del usuario autenticado (lo emite Identity).
/// </summary>
public sealed class OrganizationResolutionMiddleware(RequestDelegate next, IOptions<TenancyOptions> options)
{
    public const string OrganizationClaim = "org_id";
    public const string DevelopmentHeader = "X-Nexgate-Organization";

    public Task InvokeAsync(HttpContext context)
    {
        if (Resolve(context) is { } organizationId)
        {
            context.Items[OrganizationContextKeys.OrganizationId] = organizationId;
        }

        return next(context);
    }

    private Guid? Resolve(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return Guid.TryParse(context.User.FindFirst(OrganizationClaim)?.Value, out var fromClaim) ? fromClaim : null;
        }

        if (options.Value.DevelopmentAccess.Enabled
            && context.Request.Headers.TryGetValue(DevelopmentHeader, out var header)
            && Guid.TryParse(header.ToString(), out var fromHeader)
            && fromHeader != Guid.Empty)
        {
            return fromHeader;
        }

        return null;
    }
}

public static class OrganizationResolutionExtensions
{
    public const string OrganizationContextRequiredCode = "organization_context_required";

    public static IServiceCollection AddNexgateTenancy(this IServiceCollection services)
    {
        services.AddOptions<TenancyOptions>().BindConfiguration(TenancyOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<TenancyOptions>, TenancyOptionsValidator>();
        return services;
    }

    /// <summary>Responde 403 <c>organization_context_required</c> si la solicitud no tiene organización activa.</summary>
    public static TBuilder RequireOrganization<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            if (context.HttpContext.RequestServices.GetRequiredService<ICurrentOrganization>().Id is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Se requiere una organización activa",
                    extensions: new Dictionary<string, object?> { ["code"] = OrganizationContextRequiredCode });
            }

            return await next(context);
        });

    /// <summary>Indica si los endpoints de plataforma están disponibles (por ahora, solo con el mecanismo de desarrollo).</summary>
    public static bool PlatformEndpointsEnabled(this IServiceProvider services) =>
        services.GetRequiredService<IOptions<TenancyOptions>>().Value.DevelopmentAccess.Enabled;
}
