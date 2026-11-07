using System.Text.Json;

namespace Nexgate.Modules.Tenancy.Contracts;

/// <summary>Organización tal como la ven la API y los demás módulos. No contiene datos personales.</summary>
public sealed record OrganizationDto(Guid Id, string Name, string Status);

/// <summary>Sitio tal como lo ven la API y los demás módulos.</summary>
public sealed record SiteDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Type,
    string TimeZone,
    JsonElement Configuration,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Valores públicos del tipo de sitio.</summary>
public static class SiteTypeNames
{
    public const string Building = "building";
    public const string GatedCommunity = "gated_community";
    public const string Company = "company";
    public const string House = "house";

    public static readonly IReadOnlyList<string> All = [Building, GatedCommunity, Company, House];
}

/// <summary>Códigos de error estables del módulo.</summary>
public static class TenancyErrorCodes
{
    public const string SiteNameConflict = "site_name_conflict";
}
