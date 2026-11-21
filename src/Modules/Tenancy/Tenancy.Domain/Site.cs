namespace Nexgate.Modules.Tenancy.Domain;

public enum SiteType
{
    Building,
    GatedCommunity,
    Company,
    House
}

/// <summary>Edificio, country, empresa o casa dentro de una organización (D-02).</summary>
public sealed class Site
{
    public const int NameMaxLength = 200;
    public const int TimeZoneMaxLength = 64;
    public const string EmptyConfiguration = "{}";

    // Para la materialización desde la base.
    private Site()
    {
        Name = TimeZone = Configuration = string.Empty;
    }

    private Site(Guid id, Guid organizationId, string name, SiteType type, string timeZone, string configuration, DateTimeOffset now)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name;
        Type = type;
        TimeZone = timeZone;
        Configuration = configuration;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    /// <summary>Organización dueña. No cambia nunca después de crear el sitio.</summary>
    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; }

    public SiteType Type { get; private set; }

    /// <summary>Identificador IANA (por ejemplo, America/Argentina/Cordoba). Lo valida la capa de aplicación.</summary>
    public string TimeZone { get; private set; }

    /// <summary>Objeto JSON con la configuración del sitio. Su esquema lo definen los cambios que la usan.</summary>
    public string Configuration { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Site Create(Guid organizationId, string name, SiteType type, string timeZone, string? configuration, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(OrganizationId), "El sitio debe pertenecer a una organización.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainValidationException(nameof(Type), "Tipo de sitio inválido.");
        }

        return new Site(
            Guid.CreateVersion7(now),
            organizationId,
            DomainRules.RequiredText(name, NameMaxLength, nameof(Name)),
            type,
            DomainRules.RequiredText(timeZone, TimeZoneMaxLength, nameof(TimeZone)),
            configuration ?? EmptyConfiguration,
            now);
    }

    public void Update(string name, string timeZone, string? configuration, DateTimeOffset now)
    {
        Name = DomainRules.RequiredText(name, NameMaxLength, nameof(Name));
        TimeZone = DomainRules.RequiredText(timeZone, TimeZoneMaxLength, nameof(TimeZone));
        Configuration = configuration ?? Configuration;
        UpdatedAt = now;
    }
}
