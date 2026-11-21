using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Domain.Tenancy;

/// <summary>Conversión entre el tipo de sitio del dominio y su valor público (<see cref="SiteTypeNames"/>).</summary>
public static class SiteTypes
{
    public static bool TryParse(string? value, out SiteType type)
    {
        (var ok, type) = value switch
        {
            SiteTypeNames.Building => (true, SiteType.Building),
            SiteTypeNames.GatedCommunity => (true, SiteType.GatedCommunity),
            SiteTypeNames.Company => (true, SiteType.Company),
            SiteTypeNames.House => (true, SiteType.House),
            _ => (false, default)
        };
        return ok;
    }

    public static string ToName(SiteType type) => type switch
    {
        SiteType.Building => SiteTypeNames.Building,
        SiteType.GatedCommunity => SiteTypeNames.GatedCommunity,
        SiteType.Company => SiteTypeNames.Company,
        SiteType.House => SiteTypeNames.House,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
