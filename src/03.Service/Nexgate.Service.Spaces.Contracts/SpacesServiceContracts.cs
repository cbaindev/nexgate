namespace Nexgate.Service.Spaces.Contracts;

public enum SpacesErrorKind
{
    Validation,
    NotFound,
    Conflict
}

/// <summary>Error esperado de un caso de uso. <see cref="Code"/> es el código estable de los conflictos.</summary>
public sealed record SpacesError(SpacesErrorKind Kind, string? Field = null, string? Message = null, string? Code = null);

/// <summary>Resultado de un caso de uso: un valor o un error esperado (los errores de programación son excepciones).</summary>
public sealed record SpacesResult<T>(T? Value, SpacesError? Error)
{
    public static SpacesResult<T> Ok(T value) => new(value, null);

    public static SpacesResult<T> Fail(SpacesError error) => new(default, error);

    public static SpacesResult<T> NotFound() => new(default, new SpacesError(SpacesErrorKind.NotFound));
}

public sealed record SpaceInput(string Name, string Type, Guid? ParentId, bool IsCommon);

public sealed record SpaceUpdate(string Name, string Type, bool IsCommon);

public sealed record AccessPointInput(string Name, string Type, Guid ToSpaceId, Guid? FromSpaceId, string FailMode);

/// <summary>Árbol de espacios de los sitios de la organización activa.</summary>
public interface ISpaceService
{
    /// <summary>Espacios del sitio en orden de árbol; NotFound si el sitio no existe en la organización activa.</summary>
    Task<SpacesResult<IReadOnlyList<SpaceDto>>> ListAsync(Guid siteId, CancellationToken cancellationToken);

    Task<SpacesResult<SpaceDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<SpacesResult<SpaceDto>> CreateAsync(Guid siteId, SpaceInput input, CancellationToken cancellationToken);

    Task<SpacesResult<SpaceDto>> UpdateAsync(Guid id, SpaceUpdate update, CancellationToken cancellationToken);

    Task<SpacesResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Puntos del camino al espacio, desde la raíz.</summary>
    Task<SpacesResult<IReadOnlyList<AccessPathEntryDto>>> GetAccessPathAsync(Guid spaceId, CancellationToken cancellationToken);
}

/// <summary>Puntos de acceso de los sitios de la organización activa.</summary>
public interface IAccessPointService
{
    /// <summary>Puntos del sitio ordenados por nombre; NotFound si el sitio no existe en la organización activa.</summary>
    Task<SpacesResult<IReadOnlyList<AccessPointDto>>> ListAsync(Guid siteId, CancellationToken cancellationToken);

    Task<SpacesResult<AccessPointDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<SpacesResult<AccessPointDto>> CreateAsync(Guid siteId, AccessPointInput input, CancellationToken cancellationToken);

    Task<SpacesResult<AccessPointDto>> UpdateAsync(Guid id, AccessPointInput input, CancellationToken cancellationToken);

    Task<SpacesResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
