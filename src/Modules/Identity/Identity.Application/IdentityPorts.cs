using Nexgate.Modules.Identity.Domain;

namespace Nexgate.Modules.Identity.Application;

/// <summary>Cuenta encontrada por correo antes de autenticar (función restringida a una sola fila).</summary>
public sealed record LoginRecord(Guid UserId, string PasswordHash, AccountStatus Status, bool PlatformOperator);

/// <summary>Token de renovación encontrado por su hash, con el estado de su sesión.</summary>
public sealed record RefreshTokenRecord(
    Guid TokenId,
    Guid SessionId,
    Guid UserId,
    DateTimeOffset? UsedAt,
    DateTimeOffset? SessionRevokedAt,
    DateTimeOffset SessionExpiresAt,
    Guid? ActiveOrganizationId);

/// <summary>Invitación vigente (no aceptada ni vencida) encontrada por el hash de su token.</summary>
public sealed record InvitationRecord(Guid InvitationId, Guid OrganizationId, string NormalizedEmail);

/// <summary>
/// Búsquedas previas a la autenticación. Cada una devuelve como mucho una fila por clave exacta: no hay forma de
/// listar (design, decisión 6).
/// </summary>
public interface IIdentityLookups
{
    Task<LoginRecord?> FindLoginAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<RefreshTokenRecord?> FindRefreshTokenAsync(byte[] tokenHash, CancellationToken cancellationToken);

    Task<InvitationRecord?> FindInvitationAsync(byte[] tokenHash, CancellationToken cancellationToken);
}

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded
}

public interface IPasswordHashing
{
    string Hash(string password);

    PasswordCheck Verify(string passwordHash, string password);

    /// <summary>Verifica contra un hash ficticio, para que una cuenta inexistente tarde lo mismo que una existente.</summary>
    void VerifyAgainstDummy(string password);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface IAccessTokenIssuer
{
    AccessToken Issue(Guid userId, Guid? organizationId, Guid sessionId);
}

/// <summary>Duraciones configurables (sección <c>Identity:Tokens</c>).</summary>
public sealed record IdentityLifetimes(TimeSpan RefreshSession, TimeSpan Invitation);

/// <summary>Operaciones sobre cuentas, siempre en nombre del usuario indicado (RLS por usuario).</summary>
public interface IAccountStore
{
    Task<UserAccount?> FindOwnAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Organizaciones donde el usuario tiene membresía activa (política <c>membership_own</c>).</summary>
    Task<IReadOnlyList<Guid>> ListMembershipOrganizationsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Crea la cuenta. Devuelve false si el correo ya tiene cuenta.</summary>
    Task<bool> CreateAsync(UserAccount account, CancellationToken cancellationToken);

    Task UpdatePasswordHashAsync(Guid userId, string passwordHash, CancellationToken cancellationToken);

    Task GrantPlatformOperatorAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Sesiones de renovación, siempre en nombre de su usuario.</summary>
public interface ISessionStore
{
    Task CreateAsync(RefreshSession session, RefreshToken token, CancellationToken cancellationToken);

    /// <summary>
    /// Marca el token como usado solo si nadie lo usó antes, guarda el siguiente y la organización activa.
    /// Devuelve false si otro uso se adelantó (reutilización).
    /// </summary>
    Task<bool> RotateAsync(RefreshTokenRecord current, RefreshToken next, Guid? activeOrganizationId, DateTimeOffset now,
        CancellationToken cancellationToken);

    Task RevokeAsync(Guid userId, Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken);
}

public enum AcceptOutcome
{
    Accepted,
    InvalidInvitation
}

public interface IInvitationStore
{
    /// <summary>Guarda la invitación en la organización activa de la solicitud.</summary>
    Task AddAsync(Invitation invitation, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda la invitación en la organización indicada actuando como <paramref name="actingUserId"/>.
    /// Devuelve false si la organización no existe.
    /// </summary>
    Task<bool> AddForOrganizationAsync(Invitation invitation, Guid actingUserId, CancellationToken cancellationToken);

    /// <summary>
    /// En una sola transacción, en la organización de la invitación y como <paramref name="userId"/>: crea la cuenta
    /// (si se indica), consume la invitación, crea la membresía si falta y agrega la asignación de rol.
    /// </summary>
    Task<AcceptOutcome> AcceptAsync(InvitationRecord invitation, Guid userId, UserAccount? newAccount, DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed record MemberRecord(Guid UserId, string Email, IReadOnlyList<RoleAssignment> RoleAssignments);

public enum RemovalOutcome
{
    Removed,
    NotFound,
    LastAdministrator
}

/// <summary>Miembros de la organización activa de la solicitud.</summary>
public interface IMemberStore
{
    Task<IReadOnlyList<MemberRecord>> ListAsync(CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(Guid userId, CancellationToken cancellationToken);

    Task AddRoleAssignmentAsync(RoleAssignment assignment, CancellationToken cancellationToken);

    /// <summary>Baja con la regla del último administrador, bajo un lock por organización.</summary>
    Task<RemovalOutcome> RemoveMemberAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Revocación con la regla del último administrador, bajo un lock por organización.</summary>
    Task<RemovalOutcome> RevokeRoleAssignmentAsync(Guid userId, Guid assignmentId, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Nombre de una organización donde el usuario es miembro (verificado por quien llama).</summary>
public interface IOrganizationNames
{
    Task<string?> GetNameAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);
}
