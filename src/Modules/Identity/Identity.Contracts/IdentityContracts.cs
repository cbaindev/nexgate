namespace Nexgate.Modules.Identity.Contracts;

/// <summary>
/// Permisos administrativos sobre la API (no son acceso físico: ese es exclusivo del motor de Otorgamientos).
/// Los demás módulos los exigen con <c>RequirePermission</c>.
/// </summary>
public static class Permissions
{
    public const string OrganizationRead = "organization.read";
    public const string SitesRead = "sites.read";
    public const string SitesManage = "sites.manage";
    public const string MembersRead = "members.read";
    public const string MembersManage = "members.manage";
}

/// <summary>Valores públicos de los roles (D-05).</summary>
public static class RoleNames
{
    public const string Administrator = "administrator";
    public const string Guard = "guard";
    public const string Resident = "resident";
    public const string Employee = "employee";

    public static readonly IReadOnlyList<string> All = [Administrator, Guard, Resident, Employee];
}

/// <summary>Valores públicos del tipo de alcance de un rol (D-03). El alcance por espacio llega con Spaces.</summary>
public static class ScopeTypeNames
{
    public const string Organization = "organization";
    public const string Site = "site";

    public static readonly IReadOnlyList<string> All = [Organization, Site];
}

/// <summary>Códigos de error estables del módulo.</summary>
public static class IdentityErrorCodes
{
    public const string InvalidCredentials = "invalid_credentials";
    public const string InvalidRefreshToken = "invalid_refresh_token";
    public const string OrganizationNotAvailable = "organization_not_available";
    public const string InvalidInvitation = "invalid_invitation";
    public const string LastAdministrator = "last_administrator";
    public const string TooManyRequests = "too_many_requests";
}

/// <summary>Reglas del correo de una cuenta. Las comparte la validación de entrada de todos los módulos.</summary>
public static class EmailRules
{
    public const int MaxLength = 320;

    /// <summary>
    /// Normaliza (sin espacios alrededor, en minúsculas) y valida: una sola arroba con texto a ambos lados,
    /// sin espacios internos y de hasta <see cref="MaxLength"/> caracteres.
    /// </summary>
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        var at = normalized.IndexOf('@');
        return normalized.Length is > 0 and <= MaxLength
               && at > 0
               && at == normalized.LastIndexOf('@')
               && at < normalized.Length - 1
               && !normalized.Any(char.IsWhiteSpace);
    }
}

/// <summary>Invitación emitida. El token se devuelve una sola vez y no se guarda en claro.</summary>
public sealed record IssuedInvitation(Guid Id, string Email, string Role, string ScopeType, Guid? SiteId, DateTimeOffset ExpiresAt, string Token);

public enum AdministratorInvitationStatus
{
    Created,
    InvalidEmail,
    OrganizationNotFound
}

public sealed record AdministratorInvitationResult(AdministratorInvitationStatus Status, IssuedInvitation? Invitation);

/// <summary>
/// Invitación del administrador de una organización, emitida por un operador de plataforma. No le da al operador
/// ningún acceso a los datos de la organización.
/// </summary>
public interface IAdministratorInvitations
{
    Task<AdministratorInvitationResult> CreateAsync(Guid organizationId, string email, Guid createdBy, CancellationToken cancellationToken);
}
