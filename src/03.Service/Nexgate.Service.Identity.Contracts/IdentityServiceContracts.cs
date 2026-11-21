namespace Nexgate.Service.Identity.Contracts;

public enum IdentityErrorKind
{
    Validation,
    NotFound,
    Conflict,
    InvalidCredentials,
    OrganizationNotAvailable,
    InvalidRefreshToken,
    InvalidInvitation,
    AuthenticationRequired
}

public sealed record IdentityError(IdentityErrorKind Kind, string? Field = null, string? Message = null);

/// <summary>Resultado de un caso de uso: un valor o un error esperado (los errores de programación son excepciones).</summary>
public sealed record IdentityResult<T>(T? Value, IdentityError? Error)
{
    public static IdentityResult<T> Ok(T value) => new(value, null);

    public static IdentityResult<T> Fail(IdentityErrorKind kind, string? field = null, string? message = null) =>
        new(default, new IdentityError(kind, field, message));
}

public sealed record TokenPair(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, Guid? OrganizationId);

public sealed record InvitationInput(string? Email, string? Role, string? ScopeType, Guid? SiteId);

public sealed record AcceptedInvitation(Guid UserId, Guid OrganizationId);

public sealed record RoleAssignmentInput(string? Role, string? ScopeType, Guid? SiteId, DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil);

/// <summary>Asignación de rol con sus valores públicos (<see cref="RoleNames"/>, <see cref="ScopeTypeNames"/>).</summary>
public sealed record RoleAssignmentDto(Guid Id, string Role, string ScopeType, Guid? SiteId, DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil);

public sealed record MemberDto(Guid UserId, string Email, IReadOnlyList<RoleAssignmentDto> RoleAssignments);

public enum RemovalOutcome
{
    Removed,
    NotFound,
    LastAdministrator
}

public sealed record OrganizationSummary(Guid Id, string Name);

public sealed record CurrentUserInfo(Guid Id, string Email, Guid? ActiveOrganizationId, IReadOnlyList<OrganizationSummary> Organizations);

public enum PlatformOperatorOutcome
{
    Created,
    Promoted
}

/// <summary>Inicio de sesión, renovación con rotación y cierre de sesión.</summary>
public interface IAuthenticationService
{
    Task<IdentityResult<TokenPair>> SignInAsync(string? email, string? password, Guid? organizationId, CancellationToken cancellationToken);

    Task<IdentityResult<TokenPair>> RefreshAsync(string? refreshToken, Guid? organizationId, CancellationToken cancellationToken);

    Task SignOutAsync(string? refreshToken, CancellationToken cancellationToken);
}

/// <summary>Invitaciones a la organización activa y su aceptación.</summary>
public interface IInvitationService
{
    Task<IdentityResult<IssuedInvitation>> InviteAsync(Guid organizationId, Guid createdBy, InvitationInput input,
        CancellationToken cancellationToken);

    Task<IdentityResult<AcceptedInvitation>> AcceptAsync(string? token, string? password, Guid? currentUserId,
        CancellationToken cancellationToken);
}

/// <summary>Miembros y asignaciones de rol de la organización activa.</summary>
public interface IMemberService
{
    Task<IReadOnlyList<MemberDto>> ListAsync(CancellationToken cancellationToken);

    Task<IdentityResult<RoleAssignmentDto>> AssignRoleAsync(Guid organizationId, Guid userId, RoleAssignmentInput input,
        CancellationToken cancellationToken);

    Task<RemovalOutcome> RemoveMemberAsync(Guid userId, CancellationToken cancellationToken);

    Task<RemovalOutcome> RevokeRoleAsync(Guid userId, Guid assignmentId, CancellationToken cancellationToken);
}

/// <summary>Datos del usuario actual y sus organizaciones, para el selector.</summary>
public interface ICurrentUserService
{
    Task<CurrentUserInfo?> GetAsync(Guid userId, Guid? activeOrganizationId, CancellationToken cancellationToken);
}

/// <summary>Alta del operador de plataforma (comando administrativo, sin endpoint).</summary>
public interface IPlatformOperators
{
    Task<IdentityResult<PlatformOperatorOutcome>> EnsureAsync(string? email, string? password, CancellationToken cancellationToken);
}
