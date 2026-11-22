using Nexgate.Domain.Identity;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Data.Identity;

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

/// <summary>Consultas de la resolución de acceso de cada solicitud (pasan por RLS).</summary>
public interface IAccessQueries
{
    /// <summary>Asignaciones de rol del usuario en la organización activa, o null si no tiene membresía activa.</summary>
    Task<IReadOnlyList<RoleAssignment>?> FindMembershipAssignmentsAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);

    /// <summary>El usuario es operador de plataforma con la cuenta activa.</summary>
    Task<bool> IsPlatformOperatorAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// El espacio es alcance de alguna asignación de rol (de cualquier vigencia) o de una invitación ni aceptada ni
    /// vencida, en la organización activa.
    /// </summary>
    Task<bool> IsSpaceScopeInUseAsync(Guid spaceId, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>
/// Referencias a datos de Tenancy que Identity verifica dentro de sus propias transacciones (organización y sitio de una
/// invitación). Lo implementa la capa de servicio con los contratos de Tenancy: la persistencia no conoce otros
/// módulos. Se resuelve en el alcance de la operación, así que pasa por RLS con la organización fijada.
/// </summary>
public interface IOrganizationReferences
{
    Task<bool> ActiveOrganizationExistsAsync(CancellationToken cancellationToken);

    Task<bool> SiteExistsAsync(Guid siteId, CancellationToken cancellationToken);

    Task<bool> SpaceExistsAsync(Guid spaceId, CancellationToken cancellationToken);
}
