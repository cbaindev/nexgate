using System.ComponentModel.DataAnnotations;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Models.Identity;

// Los atributos de validación van sobre los parámetros del constructor: los controllers de ASP.NET Core los leen de ahí en los records.

/// <summary>Con <c>RefreshTokenInCookie</c>, el token de renovación viaja solo en la cookie httpOnly (aplicación web).</summary>
public sealed record SignInRequest(string? Email, string? Password, Guid? OrganizationId, bool? RefreshTokenInCookie = null);

/// <summary>Sin <c>RefreshToken</c> se usa la cookie de renovación.</summary>
public sealed record RefreshRequest(string? RefreshToken, Guid? OrganizationId);

/// <summary>Sin <c>RefreshToken</c> se usa la cookie de renovación.</summary>
public sealed record SignOutRequest(string? RefreshToken);

/// <summary>Con la cookie de renovación, <c>RefreshToken</c> es nulo.</summary>
public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string? RefreshToken, Guid? OrganizationId);

public sealed record InviteRequest(
    [Required(ErrorMessage = "El correo es obligatorio")]
    [StringLength(EmailRules.MaxLength, ErrorMessage = "El correo no puede superar 320 caracteres")]
    string? Email,
    [Required(ErrorMessage = "El rol es obligatorio")]
    string? Role,
    [Required(ErrorMessage = "El tipo de alcance es obligatorio")]
    string? ScopeType,
    Guid? SiteId,
    Guid? SpaceId);

public sealed record AcceptInvitationRequest(string? Token, string? Password);

public sealed record AcceptInvitationResponse(Guid UserId, Guid OrganizationId);

public sealed record AssignRoleRequest(
    [Required(ErrorMessage = "El rol es obligatorio")]
    string? Role,
    [Required(ErrorMessage = "El tipo de alcance es obligatorio")]
    string? ScopeType,
    Guid? SiteId,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    Guid? SpaceId);

public sealed record AdministratorInvitationRequest(
    [Required(ErrorMessage = "El correo es obligatorio")]
    [StringLength(EmailRules.MaxLength, ErrorMessage = "El correo no puede superar 320 caracteres")]
    string? Email);

public sealed record RoleAssignmentResponse(
    Guid Id,
    string Role,
    string ScopeType,
    Guid? SiteId,
    Guid? SpaceId,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil)
{
    public static RoleAssignmentResponse From(RoleAssignmentDto assignment) =>
        new(assignment.Id, assignment.Role, assignment.ScopeType, assignment.SiteId, assignment.SpaceId, assignment.ValidFrom,
            assignment.ValidUntil);
}

public sealed record MemberResponse(Guid UserId, string Email, IReadOnlyList<RoleAssignmentResponse> RoleAssignments);

/// <summary>
/// Un permiso efectivo: con alcance de organización, o en sitios enteros (<see cref="SiteIds"/>) y en espacios con su
/// subárbol (<see cref="Spaces"/>).
/// </summary>
public sealed record PermissionResponse(string Name, bool Organization, IReadOnlyList<Guid> SiteIds, IReadOnlyList<PermissionSpaceResponse> Spaces);

public sealed record PermissionSpaceResponse(Guid SpaceId, Guid SiteId);

public sealed record PermissionsResponse(Guid? OrganizationId, IReadOnlyList<PermissionResponse> Permissions);
