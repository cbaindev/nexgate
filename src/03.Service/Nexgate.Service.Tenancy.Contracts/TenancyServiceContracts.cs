namespace Nexgate.Service.Tenancy.Contracts;

public enum TenancyErrorKind
{
    Validation,
    NotFound,
    Conflict
}

public sealed record TenancyError(TenancyErrorKind Kind, string? Field = null, string? Message = null);

/// <summary>Resultado de un caso de uso: un valor o un error esperado (los errores de programación son excepciones).</summary>
public sealed record TenancyResult<T>(T? Value, TenancyError? Error)
{
    public static TenancyResult<T> Ok(T value) => new(value, null);

    public static TenancyResult<T> Fail(TenancyErrorKind kind, string? field = null, string? message = null) =>
        new(default, new TenancyError(kind, field, message));
}

public sealed record SiteInput(string Name, string Type, string TimeZone, string? Configuration);

public sealed record SiteUpdate(string Name, string TimeZone, string? Configuration);

/// <summary>Organizaciones: alta como operación de plataforma y lectura de la organización activa.</summary>
public interface IOrganizationService
{
    Task<TenancyResult<OrganizationDto>> CreateAsync(string name, CancellationToken cancellationToken);

    Task<TenancyResult<OrganizationDto>> GetActiveAsync(CancellationToken cancellationToken);
}

/// <summary>Sitios de la organización activa.</summary>
public interface ISiteService
{
    /// <summary>Sitios de la organización activa; con <paramref name="onlySites"/>, solo esos (alcance de sitio).</summary>
    Task<IReadOnlyList<SiteDto>> ListAsync(IReadOnlyCollection<Guid>? onlySites, CancellationToken cancellationToken);

    Task<TenancyResult<SiteDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Crea el sitio en la organización activa, que recibe el llamador (nunca del contenido de la solicitud).</summary>
    Task<TenancyResult<SiteDto>> CreateAsync(Guid activeOrganizationId, SiteInput input, CancellationToken cancellationToken);

    Task<TenancyResult<SiteDto>> UpdateAsync(Guid id, SiteUpdate update, CancellationToken cancellationToken);
}

/// <summary>Zonas horarias IANA válidas según la base tzdb incluida (no depende del sistema operativo).</summary>
public interface ITimeZoneCatalog
{
    bool IsValid(string? id);
}
