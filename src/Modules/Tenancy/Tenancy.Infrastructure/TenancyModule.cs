using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexgate.BuildingBlocks.Persistence;
using Nexgate.BuildingBlocks.Tenancy;
using Nexgate.BuildingBlocks.Validation;
using Nexgate.Modules.Tenancy.Application;
using Nexgate.Modules.Tenancy.Contracts;
using Nexgate.Modules.Tenancy.Domain;

namespace Nexgate.Modules.Tenancy.Infrastructure;

public sealed record CreateOrganizationRequest(
    [property: Required(ErrorMessage = "El nombre es obligatorio")]
    [property: StringLength(Organization.NameMaxLength, ErrorMessage = "El nombre no puede superar 200 caracteres")]
    string? Name);

public sealed record CreateSiteRequest(
    [property: Required(ErrorMessage = "El nombre es obligatorio")]
    [property: StringLength(Site.NameMaxLength, ErrorMessage = "El nombre no puede superar 200 caracteres")]
    string? Name,
    [property: Required(ErrorMessage = "El tipo es obligatorio")]
    [property: AllowedValues(SiteTypeNames.Building, SiteTypeNames.GatedCommunity, SiteTypeNames.Company, SiteTypeNames.House,
        ErrorMessage = "Tipo de sitio inválido: building, gated_community, company o house")]
    string? Type,
    [property: Required(ErrorMessage = "La zona horaria es obligatoria")]
    [property: IanaTimeZone]
    string? TimeZone,
    [property: JsonObjectOrNull]
    JsonElement? Configuration);

public sealed record UpdateSiteRequest(
    [property: Required(ErrorMessage = "El nombre es obligatorio")]
    [property: StringLength(Site.NameMaxLength, ErrorMessage = "El nombre no puede superar 200 caracteres")]
    string? Name,
    [property: Required(ErrorMessage = "La zona horaria es obligatoria")]
    [property: IanaTimeZone]
    string? TimeZone,
    [property: JsonObjectOrNull]
    JsonElement? Configuration);

public sealed class IanaTimeZoneAttribute() : ValidationAttribute("Zona horaria IANA inválida (por ejemplo, America/Argentina/Cordoba)")
{
    public override bool IsValid(object? value) => value is null || TimeZones.IsValid(value as string);
}

public sealed class JsonObjectOrNullAttribute() : ValidationAttribute("La configuración debe ser un objeto JSON")
{
    public override bool IsValid(object? value) =>
        value is null || value is JsonElement { ValueKind: JsonValueKind.Object or JsonValueKind.Null or JsonValueKind.Undefined };
}

/// <summary>Módulo Tenancy: organizaciones y sitios (D-02).</summary>
public static class TenancyModule
{
    public static IServiceCollection AddTenancyModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<TenancyDbContext>(TenancyDbContext.Schema);
        services.AddScoped<IOrganizationStore, OrganizationStore>();
        services.AddScoped<ISiteStore, SiteStore>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<SiteService>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    public static IEndpointRouteBuilder MapTenancyEndpoints(this IEndpointRouteBuilder api)
    {
        // Operación de plataforma. Hasta que exista el rol PlatformAdmin (add-identity-and-roles) solo está
        // disponible con el mecanismo de desarrollo; sin él la ruta no existe.
        if (api.ServiceProvider.PlatformEndpointsEnabled())
        {
            api.MapPost("/platform/organizations", async (CreateOrganizationRequest request, OrganizationService organizations, CancellationToken ct) =>
                    ToHttp(await organizations.CreateAsync(request.Name!, ct),
                        created => Results.Created("/api/v1/organization", created)))
                .WithValidation<CreateOrganizationRequest>();
        }

        api.MapGet("/organization", async (OrganizationService organizations, CancellationToken ct) =>
                ToHttp(await organizations.GetActiveAsync(ct), Results.Ok))
            .RequireOrganization();

        var sites = api.MapGroup("/sites").RequireOrganization();

        sites.MapGet("/", async (SiteService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)));

        sites.MapGet("/{id:guid}", async (Guid id, SiteService service, CancellationToken ct) =>
            ToHttp(await service.GetAsync(id, ct), Results.Ok));

        sites.MapPost("/", async (CreateSiteRequest request, SiteService service, ICurrentOrganization organization, CancellationToken ct) =>
            {
                var input = new SiteInput(request.Name!, request.Type!, request.TimeZone!, ConfigurationText(request.Configuration));
                return ToHttp(await service.CreateAsync(organization.Id!.Value, input, ct),
                    site => Results.Created($"/api/v1/sites/{site.Id}", site));
            })
            .WithValidation<CreateSiteRequest>();

        sites.MapPut("/{id:guid}", async (Guid id, UpdateSiteRequest request, SiteService service, CancellationToken ct) =>
            {
                var update = new SiteUpdate(request.Name!, request.TimeZone!, ConfigurationText(request.Configuration));
                return ToHttp(await service.UpdateAsync(id, update, ct), Results.Ok);
            })
            .WithValidation<UpdateSiteRequest>();

        return api;
    }

    private static string? ConfigurationText(JsonElement? configuration) =>
        configuration is { ValueKind: JsonValueKind.Object } value ? value.GetRawText() : null;

    private static IResult ToHttp<T>(TenancyResult<T> result, Func<T, IResult> ok) => result switch
    {
        { Error: null, Value: { } value } => ok(value),
        { Error.Kind: TenancyErrorKind.Validation } => Results.ValidationProblem(
            new Dictionary<string, string[]> { [result.Error.Field ?? string.Empty] = [result.Error.Message ?? "Valor inválido"] },
            title: "Error de validación"),
        { Error.Kind: TenancyErrorKind.Conflict } => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: result.Error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = TenancyErrorCodes.SiteNameConflict }),
        // Lo inexistente y lo de otra organización responden igual (no revelar existencia).
        _ => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Recurso no encontrado")
    };
}
