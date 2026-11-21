using System.Security.Cryptography;
using System.Text;
using Nexgate.Modules.Identity.Contracts;
using Nexgate.Modules.Identity.Domain;
using Nexgate.Modules.Tenancy.Contracts;

namespace Nexgate.Modules.Identity.Application;

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

    public static IdentityResult<T> Invalid(DomainValidationException e) => Fail(IdentityErrorKind.Validation, e.Field, e.Message);
}

/// <summary>Tokens opacos (renovación, invitaciones): 256 bits aleatorios; solo se guarda su SHA-256.</summary>
public static class OpaqueTokens
{
    public static (string Token, byte[] Hash) Create()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, Hash(token));
    }

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}

public static class IdentityNames
{
    public static bool TryParseRole(string? value, out Role role)
    {
        (var ok, role) = value switch
        {
            RoleNames.Administrator => (true, Role.Administrator),
            RoleNames.Guard => (true, Role.Guard),
            RoleNames.Resident => (true, Role.Resident),
            RoleNames.Employee => (true, Role.Employee),
            _ => (false, default)
        };
        return ok;
    }

    public static string ToName(Role role) => role switch
    {
        Role.Administrator => RoleNames.Administrator,
        Role.Guard => RoleNames.Guard,
        Role.Resident => RoleNames.Resident,
        Role.Employee => RoleNames.Employee,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public static bool TryParseScope(string? value, out ScopeType scope)
    {
        (var ok, scope) = value switch
        {
            ScopeTypeNames.Organization => (true, ScopeType.Organization),
            ScopeTypeNames.Site => (true, ScopeType.Site),
            _ => (false, default)
        };
        return ok;
    }

    public static string ToName(ScopeType scope) => scope switch
    {
        ScopeType.Organization => ScopeTypeNames.Organization,
        ScopeType.Site => ScopeTypeNames.Site,
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };
}

/// <summary>
/// Catálogo de permisos por rol (design, decisión 7). Los permisos se derivan solo de las asignaciones vigentes de la
/// organización activa; con alcance de sitio cubren solo ese sitio.
/// </summary>
public static class PermissionCatalog
{
    private static readonly IReadOnlyDictionary<Role, string[]> ByRole = new Dictionary<Role, string[]>
    {
        [Role.Administrator] =
        [
            Permissions.OrganizationRead, Permissions.SitesRead, Permissions.SitesManage,
            Permissions.MembersRead, Permissions.MembersManage
        ],
        [Role.Guard] = [Permissions.OrganizationRead, Permissions.SitesRead],
        [Role.Resident] = [Permissions.OrganizationRead, Permissions.SitesRead],
        [Role.Employee] = [Permissions.OrganizationRead, Permissions.SitesRead]
    };

    public static IReadOnlyList<string> PermissionsOf(Role role) => ByRole[role];

    /// <summary>Pares (permiso, sitio) sin repetir; sitio nulo es alcance de organización.</summary>
    public static IReadOnlyList<(string Permission, Guid? SiteId)> Grants(IEnumerable<RoleAssignment> assignments, DateTimeOffset now) =>
        assignments
            .Where(a => a.IsValidAt(now))
            .SelectMany(a => ByRole[a.Role].Select(p => (Permission: p, SiteId: a.ScopeType == ScopeType.Organization ? null : a.ScopeId)))
            .Distinct()
            .ToList();
}

public sealed record TokenPair(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, Guid? OrganizationId);

/// <summary>Inicio de sesión, renovación con rotación y cierre de sesión (design, decisiones 1 a 3).</summary>
public sealed class AuthenticationService(
    IIdentityLookups lookups,
    IPasswordHashing passwords,
    IAccountStore accounts,
    ISessionStore sessions,
    IAccessTokenIssuer tokens,
    IdentityLifetimes lifetimes,
    TimeProvider clock)
{
    public async Task<IdentityResult<TokenPair>> SignInAsync(string? email, string? password, Guid? organizationId,
        CancellationToken cancellationToken)
    {
        password ??= string.Empty;
        var login = EmailRules.TryNormalize(email, out var normalized)
            ? await lookups.FindLoginAsync(normalized, cancellationToken)
            : null;

        if (login is null)
        {
            passwords.VerifyAgainstDummy(password);
            return IdentityResult<TokenPair>.Fail(IdentityErrorKind.InvalidCredentials);
        }

        var check = passwords.Verify(login.PasswordHash, password);
        if (check == PasswordCheck.Failed || login.Status != AccountStatus.Active)
        {
            return IdentityResult<TokenPair>.Fail(IdentityErrorKind.InvalidCredentials);
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            await accounts.UpdatePasswordHashAsync(login.UserId, passwords.Hash(password), cancellationToken);
        }

        var memberships = await accounts.ListMembershipOrganizationsAsync(login.UserId, cancellationToken);
        Guid? active;
        if (organizationId is { } requested)
        {
            if (!memberships.Contains(requested))
            {
                return IdentityResult<TokenPair>.Fail(IdentityErrorKind.OrganizationNotAvailable);
            }

            active = requested;
        }
        else
        {
            active = memberships.Count == 1 ? memberships[0] : null;
        }

        var now = clock.GetUtcNow();
        var session = RefreshSession.Create(login.UserId, active, now + lifetimes.RefreshSession, now);
        var (refresh, hash) = OpaqueTokens.Create();
        await sessions.CreateAsync(session, RefreshToken.Create(session.Id, login.UserId, hash, now), cancellationToken);

        var access = tokens.Issue(login.UserId, active, session.Id);
        return IdentityResult<TokenPair>.Ok(new TokenPair(access.Value, access.ExpiresAt, refresh, active));
    }

    public async Task<IdentityResult<TokenPair>> RefreshAsync(string? refreshToken, Guid? organizationId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var current = string.IsNullOrEmpty(refreshToken)
            ? null
            : await lookups.FindRefreshTokenAsync(OpaqueTokens.Hash(refreshToken), cancellationToken);

        if (current is null || current.SessionRevokedAt is not null || current.SessionExpiresAt <= now)
        {
            return IdentityResult<TokenPair>.Fail(IdentityErrorKind.InvalidRefreshToken);
        }

        if (current.UsedAt is not null)
        {
            // Reutilización de un token ya rotado: el token se filtró. Se revoca la sesión entera.
            await sessions.RevokeAsync(current.UserId, current.SessionId, now, cancellationToken);
            return IdentityResult<TokenPair>.Fail(IdentityErrorKind.InvalidRefreshToken);
        }

        var memberships = await accounts.ListMembershipOrganizationsAsync(current.UserId, cancellationToken);
        Guid? active;
        if (organizationId is { } requested)
        {
            if (!memberships.Contains(requested))
            {
                return IdentityResult<TokenPair>.Fail(IdentityErrorKind.OrganizationNotAvailable);
            }

            active = requested;
        }
        else
        {
            // Sin elección se conserva la organización de la sesión, si la membresía sigue activa.
            active = current.ActiveOrganizationId is { } kept && memberships.Contains(kept) ? kept : null;
        }

        var (refresh, hash) = OpaqueTokens.Create();
        var next = RefreshToken.Create(current.SessionId, current.UserId, hash, now);
        if (!await sessions.RotateAsync(current, next, active, now, cancellationToken))
        {
            await sessions.RevokeAsync(current.UserId, current.SessionId, now, cancellationToken);
            return IdentityResult<TokenPair>.Fail(IdentityErrorKind.InvalidRefreshToken);
        }

        var access = tokens.Issue(current.UserId, active, current.SessionId);
        return IdentityResult<TokenPair>.Ok(new TokenPair(access.Value, access.ExpiresAt, refresh, active));
    }

    /// <summary>Revoca la sesión del token. No revela si el token existía.</summary>
    public async Task SignOutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        if (await lookups.FindRefreshTokenAsync(OpaqueTokens.Hash(refreshToken), cancellationToken) is { } current)
        {
            await sessions.RevokeAsync(current.UserId, current.SessionId, clock.GetUtcNow(), cancellationToken);
        }
    }
}

public sealed record InvitationInput(string? Email, string? Role, string? ScopeType, Guid? SiteId);

public sealed record AcceptedInvitation(Guid UserId, Guid OrganizationId);

/// <summary>Invitaciones y su aceptación (design, decisión 9).</summary>
public sealed class InvitationService(
    IInvitationStore invitations,
    IIdentityLookups lookups,
    IPasswordHashing passwords,
    ISiteDirectory sites,
    IdentityLifetimes lifetimes,
    TimeProvider clock) : IAdministratorInvitations
{
    /// <summary>Invita a la organización activa. La respuesta no depende de si el correo ya tiene cuenta.</summary>
    public async Task<IdentityResult<IssuedInvitation>> InviteAsync(Guid organizationId, Guid createdBy, InvitationInput input,
        CancellationToken cancellationToken)
    {
        var parsed = await ParseAsync(input, cancellationToken);
        if (parsed.Error is not null)
        {
            return IdentityResult<IssuedInvitation>.Fail(parsed.Error.Kind, parsed.Error.Field, parsed.Error.Message);
        }

        var (email, role, scope) = parsed.Value;
        var (invitation, token) = NewInvitation(organizationId, email, role, scope, createdBy);
        await invitations.AddAsync(invitation, cancellationToken);
        return IdentityResult<IssuedInvitation>.Ok(ToIssued(invitation, token));
    }

    public async Task<AdministratorInvitationResult> CreateAsync(Guid organizationId, string email, Guid createdBy,
        CancellationToken cancellationToken)
    {
        if (!EmailRules.TryNormalize(email, out var normalized))
        {
            return new AdministratorInvitationResult(AdministratorInvitationStatus.InvalidEmail, null);
        }

        var (invitation, token) = NewInvitation(organizationId, normalized, Role.Administrator, RoleScope.Organization, createdBy);
        return await invitations.AddForOrganizationAsync(invitation, createdBy, cancellationToken)
            ? new AdministratorInvitationResult(AdministratorInvitationStatus.Created, ToIssued(invitation, token))
            : new AdministratorInvitationResult(AdministratorInvitationStatus.OrganizationNotFound, null);
    }

    public async Task<IdentityResult<AcceptedInvitation>> AcceptAsync(string? token, string? password, Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var invitation = string.IsNullOrEmpty(token)
            ? null
            : await lookups.FindInvitationAsync(OpaqueTokens.Hash(token), cancellationToken);
        if (invitation is null)
        {
            return IdentityResult<AcceptedInvitation>.Fail(IdentityErrorKind.InvalidInvitation);
        }

        var now = clock.GetUtcNow();
        var existing = await lookups.FindLoginAsync(invitation.NormalizedEmail, cancellationToken);
        Guid userId;
        UserAccount? newAccount = null;
        if (existing is null)
        {
            try
            {
                PasswordPolicy.Validate(password);
            }
            catch (DomainValidationException e)
            {
                return IdentityResult<AcceptedInvitation>.Invalid(e);
            }

            newAccount = UserAccount.Create(invitation.NormalizedEmail, passwords.Hash(password!), platformOperator: false, now);
            userId = newAccount.Id;
        }
        else if (currentUserId == existing.UserId)
        {
            userId = existing.UserId;
        }
        else
        {
            // La cuenta existe: hay que iniciar sesión con ella. La invitación sigue disponible.
            return IdentityResult<AcceptedInvitation>.Fail(IdentityErrorKind.AuthenticationRequired);
        }

        return await invitations.AcceptAsync(invitation, userId, newAccount, now, cancellationToken) == AcceptOutcome.Accepted
            ? IdentityResult<AcceptedInvitation>.Ok(new AcceptedInvitation(userId, invitation.OrganizationId))
            : IdentityResult<AcceptedInvitation>.Fail(IdentityErrorKind.InvalidInvitation);
    }

    private async Task<IdentityResult<(string Email, Role Role, RoleScope Scope)>> ParseAsync(InvitationInput input, CancellationToken cancellationToken)
    {
        if (!EmailRules.TryNormalize(input.Email, out var email))
        {
            return IdentityResult<(string, Role, RoleScope)>.Fail(IdentityErrorKind.Validation, "Email", "Correo electrónico inválido.");
        }

        var scope = await RoleScopes.ParseAsync(input.Role, input.ScopeType, input.SiteId, sites, cancellationToken);
        return scope.Error is not null
            ? IdentityResult<(string, Role, RoleScope)>.Fail(scope.Error.Kind, scope.Error.Field, scope.Error.Message)
            : IdentityResult<(string, Role, RoleScope)>.Ok((email, scope.Value.Role, scope.Value.Scope));
    }

    private (Invitation Invitation, string Token) NewInvitation(Guid organizationId, string email, Role role, RoleScope scope, Guid createdBy)
    {
        var now = clock.GetUtcNow();
        var (token, hash) = OpaqueTokens.Create();
        return (Invitation.Create(organizationId, email, role, scope, hash, now + lifetimes.Invitation, createdBy, now), token);
    }

    private static IssuedInvitation ToIssued(Invitation invitation, string token) =>
        new(invitation.Id, invitation.NormalizedEmail, IdentityNames.ToName(invitation.Role), IdentityNames.ToName(invitation.ScopeType),
            invitation.ScopeId, invitation.ExpiresAt, token);
}

public static class RoleScopes
{
    /// <summary>
    /// Valida rol y alcance. Un sitio inexistente y uno de otra organización dan el mismo error (el directorio de
    /// sitios pasa por RLS).
    /// </summary>
    public static async Task<IdentityResult<(Role Role, RoleScope Scope)>> ParseAsync(string? roleName, string? scopeName, Guid? siteId,
        ISiteDirectory sites, CancellationToken cancellationToken)
    {
        if (!IdentityNames.TryParseRole(roleName, out var role))
        {
            return IdentityResult<(Role, RoleScope)>.Fail(IdentityErrorKind.Validation, "Role", "Rol inválido.");
        }

        if (!IdentityNames.TryParseScope(scopeName, out var scopeType))
        {
            return IdentityResult<(Role, RoleScope)>.Fail(IdentityErrorKind.Validation, "ScopeType", "Tipo de alcance inválido: organization o site.");
        }

        RoleScope scope;
        try
        {
            scope = RoleScope.Create(scopeType, siteId);
        }
        catch (DomainValidationException e)
        {
            return IdentityResult<(Role, RoleScope)>.Invalid(e);
        }

        if (scope.SiteId is { } id && !await sites.ExistsAsync(id, cancellationToken))
        {
            return IdentityResult<(Role, RoleScope)>.Fail(IdentityErrorKind.Validation, "SiteId", "El sitio no existe en la organización activa.");
        }

        return IdentityResult<(Role, RoleScope)>.Ok((role, scope));
    }
}

public sealed record RoleAssignmentInput(string? Role, string? ScopeType, Guid? SiteId, DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil);

/// <summary>Miembros y asignaciones de rol de la organización activa (design, decisiones 7 y 10).</summary>
public sealed class MemberService(IMemberStore members, ISiteDirectory sites, TimeProvider clock)
{
    public Task<IReadOnlyList<MemberRecord>> ListAsync(CancellationToken cancellationToken) => members.ListAsync(cancellationToken);

    public async Task<IdentityResult<RoleAssignment>> AssignRoleAsync(Guid organizationId, Guid userId, RoleAssignmentInput input,
        CancellationToken cancellationToken)
    {
        if (!await members.IsMemberAsync(userId, cancellationToken))
        {
            return IdentityResult<RoleAssignment>.Fail(IdentityErrorKind.NotFound);
        }

        var parsed = await RoleScopes.ParseAsync(input.Role, input.ScopeType, input.SiteId, sites, cancellationToken);
        if (parsed.Error is not null)
        {
            return IdentityResult<RoleAssignment>.Fail(parsed.Error.Kind, parsed.Error.Field, parsed.Error.Message);
        }

        RoleAssignment assignment;
        try
        {
            assignment = RoleAssignment.Create(organizationId, userId, parsed.Value.Role, parsed.Value.Scope,
                input.ValidFrom, input.ValidUntil, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return IdentityResult<RoleAssignment>.Invalid(e);
        }

        await members.AddRoleAssignmentAsync(assignment, cancellationToken);
        return IdentityResult<RoleAssignment>.Ok(assignment);
    }

    public Task<RemovalOutcome> RemoveMemberAsync(Guid userId, CancellationToken cancellationToken) =>
        members.RemoveMemberAsync(userId, clock.GetUtcNow(), cancellationToken);

    public Task<RemovalOutcome> RevokeRoleAsync(Guid userId, Guid assignmentId, CancellationToken cancellationToken) =>
        members.RevokeRoleAssignmentAsync(userId, assignmentId, clock.GetUtcNow(), cancellationToken);
}

public sealed record OrganizationSummary(Guid Id, string Name);

public sealed record CurrentUserInfo(Guid Id, string Email, Guid? ActiveOrganizationId, IReadOnlyList<OrganizationSummary> Organizations);

/// <summary>Datos del usuario actual y sus organizaciones, para el selector (design, decisión 8).</summary>
public sealed class CurrentUserService(IAccountStore accounts, IOrganizationNames organizationNames)
{
    public async Task<CurrentUserInfo?> GetAsync(Guid userId, Guid? activeOrganizationId, CancellationToken cancellationToken)
    {
        if (await accounts.FindOwnAsync(userId, cancellationToken) is not { } account)
        {
            return null;
        }

        var organizations = new List<OrganizationSummary>();
        foreach (var organizationId in await accounts.ListMembershipOrganizationsAsync(userId, cancellationToken))
        {
            if (await organizationNames.GetNameAsync(userId, organizationId, cancellationToken) is { } name)
            {
                organizations.Add(new OrganizationSummary(organizationId, name));
            }
        }

        return new CurrentUserInfo(account.Id, account.Email, activeOrganizationId,
            organizations.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }
}

public enum PlatformOperatorOutcome
{
    Created,
    Promoted
}

/// <summary>Alta del operador de plataforma desde el comando del host (design, decisión 11).</summary>
public sealed class PlatformOperatorService(IIdentityLookups lookups, IAccountStore accounts, IPasswordHashing passwords, TimeProvider clock)
{
    public async Task<IdentityResult<PlatformOperatorOutcome>> EnsureAsync(string? email, string? password, CancellationToken cancellationToken)
    {
        if (!EmailRules.TryNormalize(email, out var normalized))
        {
            return IdentityResult<PlatformOperatorOutcome>.Fail(IdentityErrorKind.Validation, "Email", "Correo electrónico inválido.");
        }

        if (await lookups.FindLoginAsync(normalized, cancellationToken) is { } existing)
        {
            await accounts.GrantPlatformOperatorAsync(existing.UserId, cancellationToken);
            return IdentityResult<PlatformOperatorOutcome>.Ok(PlatformOperatorOutcome.Promoted);
        }

        try
        {
            PasswordPolicy.Validate(password);
            var account = UserAccount.Create(email!, passwords.Hash(password!), platformOperator: true, clock.GetUtcNow());
            return await accounts.CreateAsync(account, cancellationToken)
                ? IdentityResult<PlatformOperatorOutcome>.Ok(PlatformOperatorOutcome.Created)
                : IdentityResult<PlatformOperatorOutcome>.Fail(IdentityErrorKind.Conflict, "Email", "El correo ya tiene cuenta.");
        }
        catch (DomainValidationException e)
        {
            return IdentityResult<PlatformOperatorOutcome>.Invalid(e);
        }
    }
}
