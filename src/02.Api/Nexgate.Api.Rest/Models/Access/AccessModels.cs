using System.ComponentModel.DataAnnotations;
using Nexgate.Service.Access.Contracts;
using Nexgate.Service.Occupancy.Contracts;

namespace Nexgate.Api.Rest.Models.Access;

// Los atributos de validación van sobre los parámetros del constructor: los controllers de ASP.NET Core los leen de ahí en los records.

public sealed record SubjectRequest(
    [Required(ErrorMessage = "El tipo es obligatorio")]
    [AllowedValues(SubjectKindNames.Person, SubjectKindNames.Anonymous, ErrorMessage = "Tipo de sujeto inválido: person o anonymous")]
    string? Kind,
    [Required(ErrorMessage = "El alias es obligatorio")]
    [StringLength(SubjectLimits.AliasMaxLength, ErrorMessage = "El alias no puede superar 100 caracteres")]
    string? Alias);

public sealed record ScheduleWindowRequest(IReadOnlyList<string>? Days, string? From, string? To);

public sealed record ScheduleRequest(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(AccessLimits.ScheduleNameMaxLength, ErrorMessage = "El nombre no puede superar 100 caracteres")]
    string? Name,
    [Required(ErrorMessage = "Las ventanas son obligatorias")]
    IReadOnlyList<ScheduleWindowRequest>? Windows);

/// <summary>Alta de una credencial. El valor solo existe en esta solicitud: se guarda su HMAC y nunca se devuelve.</summary>
public sealed record CreateCredentialRequest(
    [Required(ErrorMessage = "El sujeto es obligatorio")]
    Guid? SubjectId,
    [Required(ErrorMessage = "El tipo es obligatorio")]
    [AllowedValues(CredentialTypeNames.Rf, CredentialTypeNames.Card, CredentialTypeNames.Pin,
        ErrorMessage = "Tipo de credencial inválido: rf, card o pin")]
    string? Type,
    [Required(ErrorMessage = "El valor es obligatorio")]
    [StringLength(AccessLimits.CredentialValueMaxLength, ErrorMessage = "El valor no puede superar 64 caracteres")]
    string? Value,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil);

public sealed record UpdateCredentialRequest(DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil);

public sealed record CreateGrantRequest(
    [Required(ErrorMessage = "El sujeto es obligatorio")]
    Guid? SubjectId,
    [Required(ErrorMessage = "El tipo de destino es obligatorio")]
    [AllowedValues(GrantTargetTypeNames.Space, GrantTargetTypeNames.AccessPoint, ErrorMessage = "Tipo de destino inválido: space o access_point")]
    string? TargetType,
    [Required(ErrorMessage = "El destino es obligatorio")]
    Guid? TargetId,
    Guid? ScheduleId,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    [Required(ErrorMessage = "El efecto es obligatorio")]
    [AllowedValues(GrantEffectNames.Allow, GrantEffectNames.Deny, ErrorMessage = "Efecto inválido: allow o deny")]
    string? Effect,
    int? MaxUses);

/// <summary>Simulación de una pasada. El valor de la credencial solo se usa para calcular su HMAC en esta solicitud.</summary>
public sealed record SimulationRequest(
    [Required(ErrorMessage = "El tipo de credencial es obligatorio")]
    [AllowedValues(CredentialTypeNames.Rf, CredentialTypeNames.Card, CredentialTypeNames.Pin,
        ErrorMessage = "Tipo de credencial inválido: rf, card o pin")]
    string? CredentialType,
    [Required(ErrorMessage = "El valor de la credencial es obligatorio")]
    [StringLength(AccessLimits.CredentialValueMaxLength, ErrorMessage = "El valor no puede superar 64 caracteres")]
    string? CredentialValue,
    DateTimeOffset? At);
