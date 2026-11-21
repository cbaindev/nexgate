using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexgate.BuildingBlocks.Access;
using Nexgate.BuildingBlocks.Tenancy;
using Nexgate.Modules.Identity.Application;
using Nexgate.Modules.Identity.Domain;
using Nexgate.Modules.Tenancy.Contracts;
using Npgsql;

namespace Nexgate.Modules.Identity.Infrastructure;

/// <summary>Búsquedas previas a la autenticación con las funciones <c>SECURITY DEFINER</c> (design, decisión 6).</summary>
internal sealed class IdentityLookups(IdentityDbContext db) : IIdentityLookups
{
    public async Task<LoginRecord?> FindLoginAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var row = await db.Database.SqlQuery<LoginRow>($"""
                SELECT user_id AS "UserId", password_hash AS "PasswordHash", status AS "Status", platform_operator AS "PlatformOperator"
                FROM identity.find_login({normalizedEmail})
                """)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new LoginRecord(row.UserId, row.PasswordHash, Enum.Parse<AccountStatus>(row.Status, true), row.PlatformOperator);
    }

    public async Task<RefreshTokenRecord?> FindRefreshTokenAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        var row = await db.Database.SqlQuery<RefreshTokenRow>($"""
                SELECT token_id AS "TokenId", session_id AS "SessionId", user_id AS "UserId", used_at AS "UsedAt",
                       session_revoked_at AS "SessionRevokedAt", session_expires_at AS "SessionExpiresAt",
                       active_organization_id AS "ActiveOrganizationId"
                FROM identity.find_refresh_token({tokenHash})
                """)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new RefreshTokenRecord(row.TokenId, row.SessionId, row.UserId, row.UsedAt, row.SessionRevokedAt, row.SessionExpiresAt,
                row.ActiveOrganizationId);
    }

    public async Task<InvitationRecord?> FindInvitationAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        var row = await db.Database.SqlQuery<InvitationRow>($"""
                SELECT invitation_id AS "InvitationId", organization_id AS "OrganizationId", normalized_email AS "NormalizedEmail"
                FROM identity.find_invitation({tokenHash})
                """)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new InvitationRecord(row.InvitationId, row.OrganizationId, row.NormalizedEmail);
    }

    private sealed record LoginRow(Guid UserId, string PasswordHash, string Status, bool PlatformOperator);

    private sealed record RefreshTokenRow(
        Guid TokenId,
        Guid SessionId,
        Guid UserId,
        DateTimeOffset? UsedAt,
        DateTimeOffset? SessionRevokedAt,
        DateTimeOffset SessionExpiresAt,
        Guid? ActiveOrganizationId);

    private sealed record InvitationRow(Guid InvitationId, Guid OrganizationId, string NormalizedEmail);
}

/// <summary>Cuentas, siempre en un alcance donde el usuario actual es el dueño de la cuenta (RLS por usuario).</summary>
internal sealed class AccountStore(IContextScopeFactory scopes) : IAccountStore
{
    public async Task<UserAccount?> FindOwnAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.Create(userId, null);
        return await Db(scope).UserAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListMembershipOrganizationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.Create(userId, null);
        // Sin organización activa el filtro global no deja ver nada; la política membership_own sí deja ver las propias.
        return await Db(scope).Memberships.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.OrganizationId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CreateAsync(UserAccount account, CancellationToken cancellationToken)
    {
        await using var scope = scopes.Create(account.Id, null);
        var db = Db(scope);
        db.UserAccounts.Add(account);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException e) when (IsDuplicateEmail(e))
        {
            return false;
        }
    }

    public async Task UpdatePasswordHashAsync(Guid userId, string passwordHash, CancellationToken cancellationToken)
    {
        await using var scope = scopes.Create(userId, null);
        await Db(scope).UserAccounts.Where(a => a.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.PasswordHash, passwordHash), cancellationToken);
    }

    public async Task GrantPlatformOperatorAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.Create(userId, null);
        await Db(scope).UserAccounts.Where(a => a.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.PlatformOperator, true), cancellationToken);
    }

    internal static bool IsDuplicateEmail(DbUpdateException e) =>
        e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: IdentityDbContext.NormalizedEmailIndex };

    private static IdentityDbContext Db(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
}

/// <summary>Sesiones de renovación, siempre en un alcance del usuario dueño de la sesión.</summary>
internal sealed class SessionStore(IContextScopeFactory scopes) : ISessionStore
{
    public async Task CreateAsync(RefreshSession session, RefreshToken token, CancellationToken cancellationToken)
    {
        await using var scope = scopes.Create(session.UserId, null);
        var db = Db(scope);
        db.RefreshSessions.Add(session);
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RotateAsync(RefreshTokenRecord current, RefreshToken next, Guid? activeOrganizationId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var scope = scopes.Create(current.UserId, null);
        var db = Db(scope);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Solo el primer uso marca el token: dos renovaciones concurrentes no pueden ganar las dos.
        var marked = await db.RefreshTokens
            .Where(t => t.Id == current.TokenId && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), cancellationToken);
        if (marked == 0)
        {
            return false;
        }

        await db.RefreshSessions
            .Where(s => s.Id == current.SessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveOrganizationId, activeOrganizationId), cancellationToken);
        db.RefreshTokens.Add(next);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task RevokeAsync(Guid userId, Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var scope = scopes.Create(userId, null);
        await Db(scope).RefreshSessions
            .Where(s => s.Id == sessionId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);
    }

    private static IdentityDbContext Db(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
}

internal sealed class InvitationStore(IdentityDbContext db, IContextScopeFactory scopes) : IInvitationStore
{
    public async Task AddAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> AddForOrganizationAsync(Invitation invitation, Guid actingUserId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.Create(actingUserId, invitation.OrganizationId);
        if (await scope.ServiceProvider.GetRequiredService<IOrganizationDirectory>().GetActiveOrganizationAsync(cancellationToken) is null)
        {
            return false;
        }

        var scoped = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        scoped.Invitations.Add(invitation);
        await scoped.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AcceptOutcome> AcceptAsync(InvitationRecord record, Guid userId, UserAccount? newAccount, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var scope = scopes.Create(userId, record.OrganizationId);
        var scoped = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await using var transaction = await scoped.Database.BeginTransactionAsync(cancellationToken);

        if (newAccount is not null)
        {
            scoped.UserAccounts.Add(newAccount);
            try
            {
                await scoped.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (AccountStore.IsDuplicateEmail(e))
            {
                // Otra aceptación creó la cuenta en paralelo: hay que iniciar sesión y volver a aceptar.
                return AcceptOutcome.InvalidInvitation;
            }
        }

        // Consumir primero: una invitación solo se acepta una vez, aunque haya aceptaciones concurrentes.
        var consumed = await scoped.Invitations
            .Where(i => i.Id == record.InvitationId && i.AcceptedAt == null && i.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.AcceptedAt, now).SetProperty(i => i.AcceptedBy, userId), cancellationToken);
        if (consumed == 0)
        {
            return AcceptOutcome.InvalidInvitation;
        }

        var invitation = await scoped.Invitations.AsNoTracking().SingleAsync(i => i.Id == record.InvitationId, cancellationToken);
        if (invitation.ScopeId is { } siteId
            && !await scope.ServiceProvider.GetRequiredService<ISiteDirectory>().ExistsAsync(siteId, cancellationToken))
        {
            return AcceptOutcome.InvalidInvitation;
        }

        if (!await scoped.Memberships.AnyAsync(m => m.UserId == userId, cancellationToken))
        {
            scoped.Memberships.Add(Membership.Create(record.OrganizationId, userId, now));
        }

        scoped.RoleAssignments.Add(RoleAssignment.Create(record.OrganizationId, userId, invitation.Role, invitation.Scope, null, null, now));
        await scoped.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AcceptOutcome.Accepted;
    }
}

/// <summary>Miembros de la organización activa de la solicitud (RLS estándar).</summary>
internal sealed class MemberStore(IdentityDbContext db, ICurrentOrganization organization) : IMemberStore
{
    public async Task<IReadOnlyList<MemberRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var members = await db.Memberships.AsNoTracking()
            .Join(db.UserAccounts.AsNoTracking(), m => m.UserId, a => a.Id, (m, a) => new { a.Id, a.Email })
            .OrderBy(x => x.Email)
            .ToListAsync(cancellationToken);
        var assignments = (await db.RoleAssignments.AsNoTracking().OrderBy(r => r.CreatedAt).ToListAsync(cancellationToken))
            .ToLookup(r => r.UserId);

        return members.Select(m => new MemberRecord(m.Id, m.Email, assignments[m.Id].ToList())).ToList();
    }

    public Task<bool> IsMemberAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Memberships.AnyAsync(m => m.UserId == userId, cancellationToken);

    public async Task AddRoleAssignmentAsync(RoleAssignment assignment, CancellationToken cancellationToken)
    {
        db.RoleAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RemovalOutcome> RemoveMemberAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOrganizationAsync(cancellationToken);

        if (!await db.Memberships.AnyAsync(m => m.UserId == userId, cancellationToken))
        {
            return RemovalOutcome.NotFound;
        }

        var administrators = await ValidAdministratorsAsync(now, cancellationToken);
        if (administrators.Any(a => a.UserId == userId) && administrators.All(a => a.UserId == userId))
        {
            return RemovalOutcome.LastAdministrator;
        }

        await db.RoleAssignments.Where(r => r.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.Memberships.Where(m => m.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RemovalOutcome.Removed;
    }

    public async Task<RemovalOutcome> RevokeRoleAssignmentAsync(Guid userId, Guid assignmentId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOrganizationAsync(cancellationToken);

        var assignment = await db.RoleAssignments.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == assignmentId && r.UserId == userId, cancellationToken);
        if (assignment is null)
        {
            return RemovalOutcome.NotFound;
        }

        if (assignment.IsOrganizationAdministratorAt(now)
            && (await ValidAdministratorsAsync(now, cancellationToken)).All(a => a.Id == assignment.Id))
        {
            return RemovalOutcome.LastAdministrator;
        }

        await db.RoleAssignments.Where(r => r.Id == assignmentId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RemovalOutcome.Removed;
    }

    /// <summary>
    /// Serializa las operaciones que pueden quitar administradores en la organización: dos revocaciones concurrentes
    /// no pueden dejarla sin administrador (design, decisión 10).
    /// </summary>
    private Task LockOrganizationAsync(CancellationToken cancellationToken)
    {
        var key = organization.Id?.ToString() ?? throw new InvalidOperationException("Sin organización activa.");
        return db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }

    private async Task<List<RoleAssignment>> ValidAdministratorsAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        (await db.RoleAssignments.AsNoTracking()
            .Where(r => r.Role == Role.Administrator && r.ScopeType == ScopeType.Organization)
            .ToListAsync(cancellationToken))
        .Where(r => r.IsOrganizationAdministratorAt(now))
        .ToList();
}

/// <summary>Lee el nombre de cada organización con esa organización activa (design, decisión 8).</summary>
internal sealed class OrganizationNames(IContextScopeFactory scopes) : IOrganizationNames
{
    public async Task<string?> GetNameAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.Create(userId, organizationId);
        var organization = await scope.ServiceProvider.GetRequiredService<IOrganizationDirectory>().GetActiveOrganizationAsync(cancellationToken);
        return organization?.Name;
    }
}

/// <summary>Membresía y permisos efectivos de la solicitud, en una sola consulta bajo RLS (design, decisión 7).</summary>
internal sealed class IdentityAccessResolver(IdentityDbContext db, TimeProvider clock) : IRequestAccessResolver
{
    public async Task<IReadOnlyList<PermissionGrant>?> ResolveAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        var membership = await db.Memberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.OrganizationId == organizationId)
            .Select(m => new { Assignments = db.RoleAssignments.Where(r => r.UserId == userId).ToList() })
            .SingleOrDefaultAsync(cancellationToken);

        return membership is null
            ? null
            : PermissionCatalog.Grants(membership.Assignments, clock.GetUtcNow())
                .Select(g => new PermissionGrant(g.Permission, g.SiteId))
                .ToList();
    }
}

/// <summary>El operador de plataforma se lee de la base en cada solicitud, no del token.</summary>
internal sealed class IdentityPlatformAccess(IdentityDbContext db, ICurrentUser user) : IPlatformAccess
{
    public async Task<bool> IsPlatformOperatorAsync(CancellationToken cancellationToken) =>
        user.Id is { } userId
        && await db.UserAccounts.AsNoTracking()
            .AnyAsync(a => a.Id == userId && a.PlatformOperator && a.Status == AccountStatus.Active, cancellationToken);
}
