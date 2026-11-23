namespace Nexgate.Service.Access.Contracts;

public sealed record ScheduleWindowDto(IReadOnlyList<string> Days, string From, string To);

public sealed record ScheduleDto(Guid Id, Guid SiteId, string Name, IReadOnlyList<ScheduleWindowDto> Windows);

/// <summary>Credencial sin su valor ni su hash: nunca se devuelven.</summary>
public sealed record CredentialDto(Guid Id, Guid SiteId, Guid SubjectId, string Type, DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil);

public sealed record GrantDto(
    Guid Id,
    Guid SiteId,
    Guid SubjectId,
    string TargetType,
    Guid TargetId,
    Guid? ScheduleId,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    string Effect,
    string Origin,
    int? RemainingUses);

public sealed record SimulationResultDto(string Decision, string Reason, string PolicyHash);

public sealed record ScheduleInput(string Name, IReadOnlyList<ScheduleWindowDto> Windows);

public sealed record CredentialInput(Guid SubjectId, string Type, string Value, DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil);

public sealed record CredentialUpdate(DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil);

public sealed record GrantInput(
    Guid SubjectId,
    string TargetType,
    Guid TargetId,
    Guid? ScheduleId,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    string Effect,
    int? MaxUses);

public enum AccessErrorKind
{
    Validation,
    NotFound,
    Conflict
}

public sealed record AccessError(AccessErrorKind Kind, string? Field = null, string? Message = null, string? Code = null);

/// <summary>Resultado de un caso de uso: un valor o un error esperado.</summary>
public sealed record AccessResult<T>(T? Value, AccessError? Error)
{
    public static AccessResult<T> Ok(T value) => new(value, null);

    public static AccessResult<T> Fail(AccessError error) => new(default, error);

    public static AccessResult<T> Invalid(string field, string message) => new(default, new AccessError(AccessErrorKind.Validation, field, message));

    public static AccessResult<T> NotFound() => new(default, new AccessError(AccessErrorKind.NotFound));
}

public interface IScheduleService
{
    Task<AccessResult<IReadOnlyList<ScheduleDto>>> ListAsync(Guid siteId, CancellationToken cancellationToken);

    Task<AccessResult<ScheduleDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<AccessResult<ScheduleDto>> CreateAsync(Guid siteId, ScheduleInput input, CancellationToken cancellationToken);

    Task<AccessResult<ScheduleDto>> UpdateAsync(Guid id, ScheduleInput input, CancellationToken cancellationToken);

    Task<AccessResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface ICredentialService
{
    Task<AccessResult<IReadOnlyList<CredentialDto>>> ListAsync(Guid siteId, Guid? subjectId, CancellationToken cancellationToken);

    Task<AccessResult<CredentialDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<AccessResult<CredentialDto>> CreateAsync(Guid siteId, CredentialInput input, CancellationToken cancellationToken);

    Task<AccessResult<CredentialDto>> UpdateAsync(Guid id, CredentialUpdate update, CancellationToken cancellationToken);

    Task<AccessResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface IGrantService
{
    Task<AccessResult<IReadOnlyList<GrantDto>>> ListAsync(Guid siteId, Guid? subjectId, CancellationToken cancellationToken);

    Task<AccessResult<GrantDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<AccessResult<GrantDto>> CreateAsync(Guid siteId, GrantInput input, Guid createdBy, CancellationToken cancellationToken);

    Task<AccessResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Política compilada de un punto de acceso y simulación de una pasada (sin eventos ni consumo de usos).</summary>
public interface IAccessPolicyService
{
    Task<AccessResult<CompiledPolicy>> CompileAsync(Guid accessPointId, CancellationToken cancellationToken);

    Task<AccessResult<SimulationResultDto>> SimulateAsync(Guid accessPointId, string credentialType, string credentialValue,
        DateTimeOffset? at, CancellationToken cancellationToken);
}

/// <summary>
/// Clave HMAC de credenciales de un sitio (derivada de la clave maestra). La recibe el dispositivo del sitio al
/// reclamarlo; nunca se registra en logs.
/// </summary>
public interface ISiteCredentialKeys
{
    byte[] GetSiteKey(Guid siteId);
}

/// <summary>Una credencial y su sujeto, sin el valor ni el hash.</summary>
public sealed record CredentialReference(Guid CredentialId, Guid SubjectId);

/// <summary>Busca la credencial del sitio de la organización activa a partir del hash que reporta un dispositivo.</summary>
public interface ICredentialLookup
{
    /// <summary>Hash en base64url como en la política; null si no corresponde a ninguna credencial del sitio.</summary>
    Task<CredentialReference?> FindByHashAsync(Guid siteId, string policyHash, CancellationToken cancellationToken);
}

/// <summary>Descuento de usos de los otorgamientos (design de add-device-sync-protocol, decisión 7).</summary>
public interface IGrantUses
{
    /// <summary>
    /// Descuenta un uso por una pasada permitida de la credencial en el punto, en el instante dado: nada si aplicaba un
    /// otorgamiento sin límite; si no, uno del otorgamiento limitado que vence antes. Devuelve false solo si el punto o
    /// la credencial no existen en la organización activa.
    /// </summary>
    Task<bool> ConsumeAsync(Guid siteId, Guid accessPointId, Guid credentialId, DateTimeOffset occurredAt, CancellationToken cancellationToken);
}
