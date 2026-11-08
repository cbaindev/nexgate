using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexgate.BuildingBlocks.Access;
using Nexgate.BuildingBlocks.OpenApi;
using Nexgate.BuildingBlocks.Persistence;
using Nexgate.BuildingBlocks.Tenancy;
using Nexgate.BuildingBlocks.Validation;
using Nexgate.Modules.Identity.Contracts;
using Nexgate.Modules.Tenancy.Application;
using Nexgate.Modules.Tenancy.Contracts;
using Nexgate.Modules.Tenancy.Domain;

namespace Nexgate.Modules.Tenancy.Infrastructure;

public sealed record CreateOrganizationRequest(
    [property: Required(ErrorMessage = "El nombre es obligatorio")]
    [property: StringLength(Organization.NameMaxLength, ErrorMessage = "El nombre no puede superar 200 caracteres")]
    string? Name,
    [property: Required(ErrorMessage = "El correo del administrador es obligatorio")]
    [property: AccountEmail]
    string? AdministratorEmail);

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

/// <summary>Correo válido según las reglas de cuentas de Identity.</summary>
public sealed class AccountEmailAttribute() : ValidationAttribute("Correo electrónico inválido")
{
    public override bool IsValid(object? value) => value is null || EmailRules.TryNormalize(value as string, out _);
}

public sealed record CreatedOrganizationResponse(Guid Id, string Name, string Status, IssuedInvitation? AdministratorInvitation);

/// <summary>Módulo Tenancy: organizaciones y sitios (D-02).</summary>
public static class TenancyModule
{
    public static IServiceCollection AddTenancyModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<TenancyDbContext>(TenancyDbContext.Schema);
        services.AddScoped<IOrganizationStore, OrganizationStore>();
        services.AddScoped<ISiteStore, SiteStore>();
        services.AddScoped<IOrganizationDirectory, OrganizationDirectory>();
        services.AddScoped<ISiteDirectory, SiteDirectory>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<SiteService>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    public static IEndpointRouteBuilder MapTenancyEndpoints(this IEndpointRouteBuilder api)
    {
        // Operación de plataforma: crea la organización y la invitación de su primer administrador. Son dos escrituras
        // en dos módulos; si la segunda falla, el operador reemite la invitación (design de add-identity-and-roles, 11).
        api.MapPost("/platform/organizations", async (CreateOrganizationRequest request, OrganizationService organizations,
                    IAdministratorInvitations invitations, ICurrentUser user, CancellationToken ct) =>
                {
                    var created = await organizations.CreateAsync(request.Name!, ct);
                    if (created is not { Error: null, Value: { } organization })
                    {
                        return ToHttp(created, Results.Ok);
                    }

                    var invitation = await invitations.CreateAsync(organization.Id, request.AdministratorEmail!, user.Id!.Value, ct);
                    return Results.Created("/api/v1/organization",
                        new CreatedOrganizationResponse(organization.Id, organization.Name, organization.Status, invitation.Invitation));
                })
            .RequirePlatformOperator()
            .WithValidation<CreateOrganizationRequest>()
            .WithTags("Platform")
            .WithName("createOrganization")
            .WithSummary("Crear una organización")
            .WithDescription("Crea la organización y la invitación de su primer administrador, que se devuelve en " +
                "administratorInvitation. Solo para el operador de plataforma.")
            .Produces<CreatedOrganizationResponse>(StatusCodes.Status201Created)
            .Problems(StatusCodes.Status409Conflict);

        api.MapGet("/organization", async (OrganizationService organizations, CancellationToken ct) =>
                ToHttp(await organizations.GetActiveAsync(ct), Results.Ok))
            .RequirePermission(Permissions.OrganizationRead)
            .WithTags("Organization")
            .WithName("getOrganization")
            .WithSummary("Organización activa")
            .Produces<OrganizationDto>()
            .Problems(StatusCodes.Status404NotFound);

        var sites = api.MapGroup("/sites").WithTags("Sites");

        // Con alcance de sitio solo se ven los sitios del alcance; un sitio fuera del alcance responde como inexistente.
        sites.MapGet("/", async (SiteService service, ICurrentPermissions permissions, CancellationToken ct) =>
                Results.Ok(await service.ListAsync(OnlySites(permissions.SitesFor(Permissions.SitesRead)), ct)))
            .RequirePermission(Permissions.SitesRead)
            .WithName("listSites")
            .WithSummary("Listar los sitios")
            .WithDescription("Con alcance de sitio, solo los sitios del alcance.")
            .Produces<List<SiteDto>>();

        sites.MapGet("/{id:guid}", async (Guid id, SiteService service, ICurrentPermissions permissions, CancellationToken ct) =>
                permissions.SitesFor(Permissions.SitesRead).Allows(id)
                    ? ToHttp(await service.GetAsync(id, ct), Results.Ok)
                    : NotFound())
            .RequirePermission(Permissions.SitesRead)
            .WithName("getSite")
            .WithSummary("Obtener un sitio")
            .WithDescription("Un sitio fuera del alcance responde igual que uno inexistente (404).")
            .Produces<SiteDto>()
            .Problems(StatusCodes.Status404NotFound);

        sites.MapPost("/", async (CreateSiteRequest request, SiteService service, ICurrentOrganization organization, CancellationToken ct) =>
            {
                var input = new SiteInput(request.Name!, request.Type!, request.TimeZone!, ConfigurationText(request.Configuration));
                return ToHttp(await service.CreateAsync(organization.Id!.Value, input, ct),
                    site => Results.Created($"/api/v1/sites/{site.Id}", site));
            })
            .RequirePermission(Permissions.SitesManage, atOrganization: true)
            .WithValidation<CreateSiteRequest>()
            .WithName("createSite")
            .WithSummary("Crear un sitio")
            .WithDescription("Exige sites.manage con alcance de organización.")
            .Produces<SiteDto>(StatusCodes.Status201Created)
            .Problems(StatusCodes.Status409Conflict);

        sites.MapPut("/{id:guid}", async (Guid id, UpdateSiteRequest request, SiteService service, ICurrentPermissions permissions,
                    CancellationToken ct) =>
            {
                if (!permissions.SitesFor(Permissions.SitesManage).Allows(id))
                {
                    // Puede verlo pero no modificarlo: 403. Ni siquiera puede verlo: igual que inexistente.
                    return permissions.SitesFor(Permissions.SitesRead).Allows(id) ? AccessProblems.PermissionDenied() : NotFound();
                }

                var update = new SiteUpdate(request.Name!, request.TimeZone!, ConfigurationText(request.Configuration));
                return ToHttp(await service.UpdateAsync(id, update, ct), Results.Ok);
            })
            .RequirePermission(Permissions.SitesManage)
            .WithValidation<UpdateSiteRequest>()
            .WithName("updateSite")
            .WithSummary("Modificar un sitio")
            .WithDescription("Un sitio visible pero fuera del alcance de sites.manage responde 403; uno no visible, 404.")
            .Produces<SiteDto>()
            .Problems(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        return api;
    }

    private static IReadOnlyCollection<Guid>? OnlySites(SiteAccess access) => access.AllSites ? null : access.SiteIds;

    private static IResult NotFound() => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Recurso no encontrado");

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
        _ => NotFound()
    };
}
