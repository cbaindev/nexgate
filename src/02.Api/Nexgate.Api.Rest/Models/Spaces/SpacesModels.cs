using System.ComponentModel.DataAnnotations;
using Nexgate.Service.Spaces.Contracts;

namespace Nexgate.Api.Rest.Models.Spaces;

// Los atributos de validación van sobre los parámetros del constructor: los controllers de ASP.NET Core los leen de ahí en los records.

public sealed record CreateSpaceRequest(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(SpaceLimits.NameMaxLength, ErrorMessage = "El nombre no puede superar 200 caracteres")]
    string? Name,
    [Required(ErrorMessage = "El tipo es obligatorio")]
    [AllowedValues(SpaceTypeNames.Site, SpaceTypeNames.Building, SpaceTypeNames.Floor, SpaceTypeNames.Unit,
        SpaceTypeNames.CommonArea, SpaceTypeNames.Parking, SpaceTypeNames.Lot, SpaceTypeNames.Zone,
        ErrorMessage = "Tipo de espacio inválido: site, building, floor, unit, common_area, parking, lot o zone")]
    string? Type,
    Guid? ParentId,
    bool? IsCommon);

public sealed record UpdateSpaceRequest(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(SpaceLimits.NameMaxLength, ErrorMessage = "El nombre no puede superar 200 caracteres")]
    string? Name,
    [Required(ErrorMessage = "El tipo es obligatorio")]
    [AllowedValues(SpaceTypeNames.Site, SpaceTypeNames.Building, SpaceTypeNames.Floor, SpaceTypeNames.Unit,
        SpaceTypeNames.CommonArea, SpaceTypeNames.Parking, SpaceTypeNames.Lot, SpaceTypeNames.Zone,
        ErrorMessage = "Tipo de espacio inválido: site, building, floor, unit, common_area, parking, lot o zone")]
    string? Type,
    bool? IsCommon);

/// <summary>Alta y modificación de un punto de acceso. El modo de fallo es obligatorio y no tiene valor por omisión.</summary>
public sealed record AccessPointRequest(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(SpaceLimits.NameMaxLength, ErrorMessage = "El nombre no puede superar 200 caracteres")]
    string? Name,
    [Required(ErrorMessage = "El tipo es obligatorio")]
    [AllowedValues(AccessPointTypeNames.Door, AccessPointTypeNames.Gate, AccessPointTypeNames.Barrier,
        AccessPointTypeNames.Turnstile, AccessPointTypeNames.Elevator,
        ErrorMessage = "Tipo de punto de acceso inválido: door, gate, barrier, turnstile o elevator")]
    string? Type,
    [Required(ErrorMessage = "El espacio al que da acceso es obligatorio")]
    Guid? ToSpaceId,
    Guid? FromSpaceId,
    [Required(ErrorMessage = "El modo de fallo es obligatorio")]
    [AllowedValues(FailModeNames.FailSafe, FailModeNames.FailSecure,
        ErrorMessage = "Modo de fallo inválido: fail_safe o fail_secure")]
    string? FailMode);
