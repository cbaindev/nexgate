using Nexgate.Modules.Identity.Contracts;

namespace Nexgate.Modules.Identity.Domain;

/// <summary>Una regla del dominio no se cumple. El campo usa el nombre de la propiedad pública.</summary>
public sealed class DomainValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

public static class EmailAddress
{
    public static string Normalize(string? value, string field = "Email") =>
        EmailRules.TryNormalize(value, out var normalized)
            ? normalized
            : throw new DomainValidationException(field, "Correo electrónico inválido.");
}

public static class PasswordPolicy
{
    public const int MinLength = 12;

    /// <summary>Solo longitud mínima: sin reglas de composición (ver design, decisión 3).</summary>
    public static void Validate(string? password, string field = "Password")
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            throw new DomainValidationException(field, $"La contraseña debe tener al menos {MinLength} caracteres.");
        }
    }
}

public enum AccountStatus
{
    Active,
    Disabled
}

/// <summary>
/// Cuenta de usuario de la plataforma. Una cuenta sirve para varias organizaciones (D-04), por eso no tiene
/// organización: es un dato de identidad aislado por usuario (design, decisión 5).
/// </summary>
public sealed class UserAccount
{
    private UserAccount()
    {
        Email = string.Empty;
        NormalizedEmail = string.Empty;
        PasswordHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; }

    public string NormalizedEmail { get; private set; }

    public string PasswordHash { get; private set; }

    public AccountStatus Status { get; private set; }

    public bool PlatformOperator { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static UserAccount Create(string email, string passwordHash, bool platformOperator, DateTimeOffset now)
    {
        var normalized = EmailAddress.Normalize(email);
        return new UserAccount
        {
            Id = Guid.CreateVersion7(now),
            Email = email.Trim(),
            NormalizedEmail = normalized,
            PasswordHash = passwordHash,
            Status = AccountStatus.Active,
            PlatformOperator = platformOperator,
            CreatedAt = now
        };
    }

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void GrantPlatformOperator() => PlatformOperator = true;
}

public enum Role
{
    Administrator,
    Guard,
    Resident,
    Employee
}

public enum ScopeType
{
    Organization,
    Site
}

/// <summary>Alcance de un rol: toda la organización o un sitio de ella (D-03).</summary>
public sealed record RoleScope(ScopeType Type, Guid? SiteId)
{
    public static readonly RoleScope Organization = new(ScopeType.Organization, null);

    public static RoleScope Site(Guid siteId) => new(ScopeType.Site, siteId);

    public static RoleScope Create(ScopeType type, Guid? siteId) => type switch
    {
        ScopeType.Organization => Organization,
        ScopeType.Site when siteId is { } id && id != Guid.Empty => Site(id),
        _ => throw new DomainValidationException("SiteId", "El alcance de sitio requiere el identificador del sitio.")
    };
}

/// <summary>Pertenencia de un usuario a una organización. Sin membresía no hay organización activa.</summary>
public sealed class Membership
{
    private Membership()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Membership Create(Guid organizationId, Guid userId, DateTimeOffset now) =>
        new() { Id = Guid.CreateVersion7(now), OrganizationId = organizationId, UserId = userId, CreatedAt = now };
}

/// <summary>Usuario + Rol + Alcance, con vigencia opcional (D-03).</summary>
public sealed class RoleAssignment
{
    private RoleAssignment()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public Role Role { get; private set; }

    public ScopeType ScopeType { get; private set; }

    public Guid? ScopeId { get; private set; }

    public DateTimeOffset? ValidFrom { get; private set; }

    public DateTimeOffset? ValidUntil { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public RoleScope Scope => new(ScopeType, ScopeId);

    public static RoleAssignment Create(Guid organizationId, Guid userId, Role role, RoleScope scope,
        DateTimeOffset? validFrom, DateTimeOffset? validUntil, DateTimeOffset now)
    {
        if (validFrom is { } from && validUntil is { } until && until <= from)
        {
            throw new DomainValidationException("ValidUntil", "La vigencia debe terminar después de empezar.");
        }

        return new RoleAssignment
        {
            Id = Guid.CreateVersion7(now),
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
            ScopeType = scope.Type,
            ScopeId = scope.SiteId,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            CreatedAt = now
        };
    }

    public bool IsValidAt(DateTimeOffset now) =>
        (ValidFrom is null || ValidFrom <= now) && (ValidUntil is null || ValidUntil > now);

    /// <summary>Administrador con alcance de organización vigente: la regla del último administrador cuenta estos.</summary>
    public bool IsOrganizationAdministratorAt(DateTimeOffset now) =>
        Role == Role.Administrator && ScopeType == ScopeType.Organization && IsValidAt(now);
}

/// <summary>Invitación a una organización con un rol y un alcance. Solo se guarda el hash del token.</summary>
public sealed class Invitation
{
    private Invitation()
    {
        NormalizedEmail = string.Empty;
        TokenHash = [];
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string NormalizedEmail { get; private set; }

    public Role Role { get; private set; }

    public ScopeType ScopeType { get; private set; }

    public Guid? ScopeId { get; private set; }

    public byte[] TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public Guid? AcceptedBy { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public RoleScope Scope => new(ScopeType, ScopeId);

    public static Invitation Create(Guid organizationId, string email, Role role, RoleScope scope, byte[] tokenHash,
        DateTimeOffset expiresAt, Guid createdBy, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            OrganizationId = organizationId,
            NormalizedEmail = EmailAddress.Normalize(email),
            Role = role,
            ScopeType = scope.Type,
            ScopeId = scope.SiteId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedBy = createdBy,
            CreatedAt = now
        };
}

/// <summary>Sesión de renovación: agrupa los tokens de renovación emitidos desde un inicio de sesión.</summary>
public sealed class RefreshSession
{
    private RefreshSession()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Organización activa de la sesión (la conserva una renovación que no elige otra). Puede ser nula.</summary>
    public Guid? ActiveOrganizationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static RefreshSession Create(Guid userId, Guid? organizationId, DateTimeOffset expiresAt, DateTimeOffset now) =>
        new() { Id = Guid.CreateVersion7(now), UserId = userId, ActiveOrganizationId = organizationId, CreatedAt = now, ExpiresAt = expiresAt };
}

/// <summary>Token de renovación de un solo uso. Solo se guarda su hash.</summary>
public sealed class RefreshToken
{
    private RefreshToken()
    {
        TokenHash = [];
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid UserId { get; private set; }

    public byte[] TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public static RefreshToken Create(Guid sessionId, Guid userId, byte[] tokenHash, DateTimeOffset now) =>
        new() { Id = Guid.CreateVersion7(now), SessionId = sessionId, UserId = userId, TokenHash = tokenHash, CreatedAt = now };
}
