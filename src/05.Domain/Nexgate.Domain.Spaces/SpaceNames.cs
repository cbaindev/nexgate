using Nexgate.Service.Spaces.Contracts;

namespace Nexgate.Domain.Spaces;

public enum SpaceType
{
    Site,
    Building,
    Floor,
    Unit,
    CommonArea,
    Parking,
    Lot,
    Zone
}

public enum AccessPointType
{
    Door,
    Gate,
    Barrier,
    Turnstile,
    Elevator
}

public enum FailMode
{
    FailSafe,
    FailSecure
}

/// <summary>Conversión entre los valores del dominio y sus valores públicos del Contracts.</summary>
public static class SpaceNames
{
    private static readonly (SpaceType Type, string Name)[] SpaceTypes =
    [
        (SpaceType.Site, SpaceTypeNames.Site), (SpaceType.Building, SpaceTypeNames.Building),
        (SpaceType.Floor, SpaceTypeNames.Floor), (SpaceType.Unit, SpaceTypeNames.Unit),
        (SpaceType.CommonArea, SpaceTypeNames.CommonArea), (SpaceType.Parking, SpaceTypeNames.Parking),
        (SpaceType.Lot, SpaceTypeNames.Lot), (SpaceType.Zone, SpaceTypeNames.Zone)
    ];

    private static readonly (AccessPointType Type, string Name)[] PointTypes =
    [
        (AccessPointType.Door, AccessPointTypeNames.Door), (AccessPointType.Gate, AccessPointTypeNames.Gate),
        (AccessPointType.Barrier, AccessPointTypeNames.Barrier), (AccessPointType.Turnstile, AccessPointTypeNames.Turnstile),
        (AccessPointType.Elevator, AccessPointTypeNames.Elevator)
    ];

    private static readonly (FailMode Mode, string Name)[] FailModes =
    [
        (FailMode.FailSafe, FailModeNames.FailSafe), (FailMode.FailSecure, FailModeNames.FailSecure)
    ];

    public static bool TryParseSpaceType(string? value, out SpaceType type) => TryParse(SpaceTypes, value, out type);

    public static string ToName(SpaceType type) => ToName(SpaceTypes, type);

    public static bool TryParseAccessPointType(string? value, out AccessPointType type) => TryParse(PointTypes, value, out type);

    public static string ToName(AccessPointType type) => ToName(PointTypes, type);

    public static bool TryParseFailMode(string? value, out FailMode mode) => TryParse(FailModes, value, out mode);

    public static string ToName(FailMode mode) => ToName(FailModes, mode);

    private static bool TryParse<T>((T Value, string Name)[] table, string? value, out T result) where T : struct, Enum
    {
        foreach (var (candidate, name) in table)
        {
            if (name == value)
            {
                result = candidate;
                return true;
            }
        }

        result = default;
        return false;
    }

    private static string ToName<T>((T Value, string Name)[] table, T value) where T : struct, Enum
    {
        foreach (var (candidate, name) in table)
        {
            if (EqualityComparer<T>.Default.Equals(candidate, value))
            {
                return name;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(value));
    }
}
