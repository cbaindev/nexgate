using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Nexgate.Api.Rest.Models.Validation;
using Nexgate.Domain.Tenancy;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Api.Rest.Models.Tenancy;

// Los atributos de validación van sobre los parámetros del constructor: los controllers de ASP.NET Core los leen de ahí en los records.

public sealed record CreateOrganizationRequest(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(Organization.NameMaxLength, ErrorMessage = "El nombre no puede superar 200 caracteres")]
    string? Name,
    [Required(ErrorMessage = "El correo del administrador es obligatorio")]
    [AccountEmail]
    string? AdministratorEmail);

public sealed record CreateSiteRequest(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(Site.NameMaxLength, ErrorMessage = "El nombre no puede superar 200 caracteres")]
    string? Name,
    [Required(ErrorMessage = "El tipo es obligatorio")]
    [AllowedValues(SiteTypeNames.Building, SiteTypeNames.GatedCommunity, SiteTypeNames.Company, SiteTypeNames.House,
        ErrorMessage = "Tipo de sitio inválido: building, gated_community, company o house")]
    string? Type,
    [Required(ErrorMessage = "La zona horaria es obligatoria")]
    [IanaTimeZone]
    string? TimeZone,
    [JsonObjectOrNull]
    JsonElement? Configuration);

public sealed record UpdateSiteRequest(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(Site.NameMaxLength, ErrorMessage = "El nombre no puede superar 200 caracteres")]
    string? Name,
    [Required(ErrorMessage = "La zona horaria es obligatoria")]
    [IanaTimeZone]
    string? TimeZone,
    [JsonObjectOrNull]
    JsonElement? Configuration);

public sealed record CreatedOrganizationResponse(Guid Id, string Name, string Status, IssuedInvitation? AdministratorInvitation);
