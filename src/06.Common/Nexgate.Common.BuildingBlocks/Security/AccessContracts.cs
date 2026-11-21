namespace Nexgate.Common.BuildingBlocks.Security;

/// <summary>Claims del token de acceso que emite Identity.</summary>
public static class AccessClaims
{
    public const string Subject = "sub";
    public const string Organization = "org_id";
    public const string Session = "sid";
}

/// <summary>Códigos de error estables de autenticación y autorización.</summary>
public static class AccessErrorCodes
{
    public const string AuthenticationRequired = "authentication_required";
    public const string OrganizationContextRequired = "organization_context_required";
    public const string PermissionDenied = "permission_denied";
}

/// <summary>
/// Verifica que el usuario tiene una membresía activa en la organización y devuelve sus permisos efectivos en ella,
/// o null si no hay membresía. Lo implementa Identity. Se ejecuta con el usuario y la organización ya fijados en el
/// contexto, así que la consulta pasa por RLS.
/// </summary>
public interface IRequestAccessResolver
{
    Task<IReadOnlyList<PermissionGrant>?> ResolveAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);
}

/// <summary>Indica si el usuario actual es operador de plataforma. Lo implementa Identity (se lee de la base).</summary>
public interface IPlatformAccess
{
    Task<bool> IsPlatformOperatorAsync(CancellationToken cancellationToken);
}
