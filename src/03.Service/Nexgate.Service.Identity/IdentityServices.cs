using System.Security.Cryptography;
using System.Text;
using Nexgate.Data.Identity;
using Nexgate.Domain.Identity;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Service.Identity;

/// <summary>Tokens opacos (renovación, invitaciones): 256 bits aleatorios; solo se guarda su SHA-256.</summary>
internal static class OpaqueTokens
{
    public static (string Token, byte[] Hash) Create()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, Hash(token));
    }

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}

/// <summary>Inicio de sesión, renovación con rotación y cierre de sesión (design, decisiones 1 a 3).</summary>
internal sealed class AuthenticationService(
    IIdentityLookups lookups,
    IPasswordHashing passwords,
    IAccountStore accounts,
    ISessionStore sessions,
    IAccessTokenIssuer tokens,
    IdentityLifetimes lifetimes,
    TimeProvider clock) : IAuthenticationService
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

/// <summary>Invitaciones y su aceptación (design, decisión 9).</summary>
internal sealed class InvitationService(
    IInvitationStore invitations,
    IIdentityLookups lookups,
    IPasswordHashing passwords,
    ScopeReferences scopes,
    IdentityLifetimes lifetimes,
    TimeProvider clock) : IInvitationService, IAdministratorInvitations
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
                return IdentityFailures.Invalid<AcceptedInvitation>(e);
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

        var scope = await RoleScopes.ParseAsync(input.Role, input.ScopeType, input.SiteId, input.SpaceId, scopes, cancellationToken);
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
            invitation.Scope.SiteId, invitation.Scope.SpaceId, invitation.ExpiresAt, token);
}

internal static class RoleScopes
{
    /// <summary>
    /// Valida rol y alcance. Un sitio o espacio inexistente y uno de otra organización dan el mismo error (los directorios
    /// pasan por RLS).
    /// </summary>
    public static async Task<IdentityResult<(Role Role, RoleScope Scope)>> ParseAsync(string? roleName, string? scopeName, Guid? siteId,
        Guid? spaceId, ScopeReferences references, CancellationToken cancellationToken)
    {
        if (!IdentityNames.TryParseRole(roleName, out var role))
        {
            return IdentityResult<(Role, RoleScope)>.Fail(IdentityErrorKind.Validation, "Role", "Rol inválido.");
        }

        if (!IdentityNames.TryParseScope(scopeName, out var scopeType))
        {
            return IdentityResult<(Role, RoleScope)>.Fail(IdentityErrorKind.Validation, "ScopeType", "Tipo de alcance inválido: organization, site o space.");
        }

        RoleScope scope;
        try
        {
            scope = RoleScope.Create(scopeType, siteId, spaceId);
        }
        catch (DomainValidationException e)
        {
            return IdentityFailures.Invalid<(Role, RoleScope)>(e);
        }

        if (scope.SiteId is { } id && !await references.SiteExistsAsync(id, cancellationToken))
        {
            return IdentityResult<(Role, RoleScope)>.Fail(IdentityErrorKind.Validation, "SiteId", "El sitio no existe en la organización activa.");
        }

        if (scope.SpaceId is { } space && !await references.SpaceExistsAsync(space, cancellationToken))
        {
            return IdentityResult<(Role, RoleScope)>.Fail(IdentityErrorKind.Validation, "SpaceId", "El espacio no existe en la organización activa.");
        }

        return IdentityResult<(Role, RoleScope)>.Ok((role, scope));
    }
}

/// <summary>Miembros y asignaciones de rol de la organización activa (design, decisiones 7 y 10).</summary>
internal sealed class MemberService(IMemberStore members, ScopeReferences scopes, TimeProvider clock) : IMemberService
{
    public async Task<IReadOnlyList<MemberDto>> ListAsync(CancellationToken cancellationToken) =>
        (await members.ListAsync(cancellationToken))
        .Select(m => new MemberDto(m.UserId, m.Email, m.RoleAssignments.Select(ToDto).ToList()))
        .ToList();

    public async Task<IdentityResult<RoleAssignmentDto>> AssignRoleAsync(Guid organizationId, Guid userId, RoleAssignmentInput input,
        CancellationToken cancellationToken)
    {
        if (!await members.IsMemberAsync(userId, cancellationToken))
        {
            return IdentityResult<RoleAssignmentDto>.Fail(IdentityErrorKind.NotFound);
        }

        var parsed = await RoleScopes.ParseAsync(input.Role, input.ScopeType, input.SiteId, input.SpaceId, scopes, cancellationToken);
        if (parsed.Error is not null)
        {
            return IdentityResult<RoleAssignmentDto>.Fail(parsed.Error.Kind, parsed.Error.Field, parsed.Error.Message);
        }

        RoleAssignment assignment;
        try
        {
            assignment = RoleAssignment.Create(organizationId, userId, parsed.Value.Role, parsed.Value.Scope,
                input.ValidFrom, input.ValidUntil, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return IdentityFailures.Invalid<RoleAssignmentDto>(e);
        }

        await members.AddRoleAssignmentAsync(assignment, cancellationToken);
        return IdentityResult<RoleAssignmentDto>.Ok(ToDto(assignment));
    }

    public Task<RemovalOutcome> RemoveMemberAsync(Guid userId, CancellationToken cancellationToken) =>
        members.RemoveMemberAsync(userId, clock.GetUtcNow(), cancellationToken);

    public Task<RemovalOutcome> RevokeRoleAsync(Guid userId, Guid assignmentId, CancellationToken cancellationToken) =>
        members.RevokeRoleAssignmentAsync(userId, assignmentId, clock.GetUtcNow(), cancellationToken);

    private static RoleAssignmentDto ToDto(RoleAssignment assignment) =>
        new(assignment.Id, IdentityNames.ToName(assignment.Role), IdentityNames.ToName(assignment.ScopeType), assignment.Scope.SiteId,
            assignment.Scope.SpaceId, assignment.ValidFrom, assignment.ValidUntil);
}

/// <summary>Datos del usuario actual y sus organizaciones, para el selector (design, decisión 8).</summary>
internal sealed class CurrentUserService(IAccountStore accounts, IOrganizationNames organizationNames) : ICurrentUserService
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

/// <summary>Alta del operador de plataforma desde el comando del host (design, decisión 11).</summary>
internal sealed class PlatformOperatorService(IIdentityLookups lookups, IAccountStore accounts, IPasswordHashing passwords, TimeProvider clock)
    : IPlatformOperators
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
            return IdentityFailures.Invalid<PlatformOperatorOutcome>(e);
        }
    }
}

internal static class IdentityFailures
{
    public static IdentityResult<T> Invalid<T>(DomainValidationException e) => IdentityResult<T>.Fail(IdentityErrorKind.Validation, e.Field, e.Message);
}
