namespace Nexgate.Service.Occupancy.Contracts;

/// <summary>
/// Sujeto: la representación local de alguien en un sitio (docs/02). No tiene datos personales; el alias lo elige quien
/// lo carga y no se registra en logs.
/// </summary>
public sealed record SubjectDto(Guid Id, Guid SiteId, string Kind, string Alias);

/// <summary>Valores públicos del tipo de sujeto.</summary>
public static class SubjectKindNames
{
    public const string Person = "person";
    public const string Anonymous = "anonymous";

    public static readonly IReadOnlyList<string> All = [Person, Anonymous];
}

public static class SubjectLimits
{
    public const int AliasMaxLength = 100;
}

/// <summary>Códigos de error estables del módulo.</summary>
public static class OccupancyErrorCodes
{
    public const string SubjectInUse = "subject_in_use";
}

public enum OccupancyErrorKind
{
    Validation,
    NotFound,
    Conflict
}

public sealed record OccupancyError(OccupancyErrorKind Kind, string? Field = null, string? Message = null, string? Code = null);

/// <summary>Resultado de un caso de uso: un valor o un error esperado.</summary>
public sealed record OccupancyResult<T>(T? Value, OccupancyError? Error)
{
    public static OccupancyResult<T> Ok(T value) => new(value, null);

    public static OccupancyResult<T> Fail(OccupancyError error) => new(default, error);

    public static OccupancyResult<T> NotFound() => new(default, new OccupancyError(OccupancyErrorKind.NotFound));
}

public sealed record SubjectInput(string Kind, string Alias);

/// <summary>Sujetos de los sitios de la organización activa.</summary>
public interface ISubjectService
{
    /// <summary>Sujetos del sitio ordenados por alias; NotFound si el sitio no existe en la organización activa.</summary>
    Task<OccupancyResult<IReadOnlyList<SubjectDto>>> ListAsync(Guid siteId, CancellationToken cancellationToken);

    Task<OccupancyResult<SubjectDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<OccupancyResult<SubjectDto>> CreateAsync(Guid siteId, SubjectInput input, Guid createdBy, CancellationToken cancellationToken);

    Task<OccupancyResult<SubjectDto>> UpdateAsync(Guid id, SubjectInput input, CancellationToken cancellationToken);

    Task<OccupancyResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Sujetos de la organización activa para otros módulos (pasa por RLS: un sujeto ajeno no existe).</summary>
public interface ISubjectDirectory
{
    Task<SubjectDto?> GetSubjectAsync(Guid subjectId, CancellationToken cancellationToken);
}

/// <summary>
/// Uso de un sujeto por otro módulo (credenciales, otorgamientos). Cada módulo que referencia sujetos lo implementa;
/// Occupancy no elimina un sujeto que alguno usa.
/// </summary>
public interface ISubjectUsage
{
    Task<bool> IsUsedAsync(Guid subjectId, CancellationToken cancellationToken);
}
