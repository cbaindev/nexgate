namespace Nexgate.Service.Spaces.Contracts;

/// <summary>Espacio tal como lo ven la API y los demás módulos. No contiene datos personales.</summary>
public sealed record SpaceDto(Guid Id, Guid SiteId, Guid? ParentId, string Type, string Name, bool IsCommon, int Depth);

/// <summary>Punto de acceso tal como lo ven la API y los demás módulos.</summary>
public sealed record AccessPointDto(Guid Id, Guid SiteId, string Name, string Type, Guid ToSpaceId, Guid? FromSpaceId, string FailMode);

/// <summary>Un punto de acceso del camino a un destino, con la profundidad del espacio al que da acceso.</summary>
public sealed record AccessPathEntryDto(
    Guid Id,
    Guid SiteId,
    string Name,
    string Type,
    Guid ToSpaceId,
    Guid? FromSpaceId,
    string FailMode,
    int ToSpaceDepth);

/// <summary>Valores públicos del tipo de espacio. <see cref="Site"/> es solo para la raíz del árbol.</summary>
public static class SpaceTypeNames
{
    public const string Site = "site";
    public const string Building = "building";
    public const string Floor = "floor";
    public const string Unit = "unit";
    public const string CommonArea = "common_area";
    public const string Parking = "parking";
    public const string Lot = "lot";
    public const string Zone = "zone";

    public static readonly IReadOnlyList<string> All = [Site, Building, Floor, Unit, CommonArea, Parking, Lot, Zone];
}

/// <summary>Valores públicos del tipo de punto de acceso.</summary>
public static class AccessPointTypeNames
{
    public const string Door = "door";
    public const string Gate = "gate";
    public const string Barrier = "barrier";
    public const string Turnstile = "turnstile";
    public const string Elevator = "elevator";

    public static readonly IReadOnlyList<string> All = [Door, Gate, Barrier, Turnstile, Elevator];
}

/// <summary>Modo de fallo de un punto de acceso: se abre sin energía (fail-safe) o queda cerrado (fail-secure).</summary>
public static class FailModeNames
{
    public const string FailSafe = "fail_safe";
    public const string FailSecure = "fail_secure";

    public static readonly IReadOnlyList<string> All = [FailSafe, FailSecure];
}

/// <summary>Límites públicos del módulo (los usa también la validación de entrada de la API).</summary>
public static class SpaceLimits
{
    public const int NameMaxLength = 200;

    /// <summary>Profundidad máxima de un espacio (la raíz tiene profundidad 0).</summary>
    public const int MaxDepth = 10;
}

/// <summary>Códigos de error estables del módulo.</summary>
public static class SpacesErrorCodes
{
    public const string SpaceRootExists = "space_root_exists";
    public const string SpaceNameConflict = "space_name_conflict";
    public const string SpaceHasDependents = "space_has_dependents";
    public const string AccessPointNameConflict = "access_point_name_conflict";
}

/// <summary>
/// Espacios y puntos de acceso de la organización activa, para otros módulos. Pasa por RLS: lo ajeno no existe.
/// <see cref="GetAccessPathAsync"/> es la regla de cobertura de D-13: un punto cubre un destino si da acceso al
/// destino o a uno de sus ancestros.
/// </summary>
public interface ISpaceDirectory
{
    Task<SpaceDto?> GetSpaceAsync(Guid spaceId, CancellationToken cancellationToken);

    Task<AccessPointDto?> GetAccessPointAsync(Guid accessPointId, CancellationToken cancellationToken);

    /// <summary>Puntos del camino al espacio, desde la raíz; null si el espacio no existe.</summary>
    Task<IReadOnlyList<AccessPathEntryDto>?> GetAccessPathAsync(Guid spaceId, CancellationToken cancellationToken);
}
