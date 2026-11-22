namespace Nexgate.Domain.Spaces;

/// <summary>
/// Puerta, portón, barrera, molinete o ascensor de un sitio. Su atributo espacial importante es el espacio hacia el
/// que da acceso; el espacio desde el que se entra es opcional. El modo de fallo se elige siempre de forma explícita.
/// </summary>
public sealed class AccessPoint
{
    // Para la materialización desde la base.
    private AccessPoint()
    {
        Name = string.Empty;
    }

    private AccessPoint(Guid id, Guid siteId, DateTimeOffset now)
    {
        Id = id;
        SiteId = siteId;
        Name = string.Empty;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    /// <summary>Organización dueña. La asigna la persistencia con la organización activa y no cambia nunca.</summary>
    public Guid OrganizationId { get; private set; }

    public Guid SiteId { get; private set; }

    public string Name { get; private set; }

    public AccessPointType Type { get; private set; }

    public Guid ToSpaceId { get; private set; }

    public Guid? FromSpaceId { get; private set; }

    public FailMode FailMode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static AccessPoint Create(Guid siteId, string name, AccessPointType type, Space to, Space? from, FailMode failMode, DateTimeOffset now)
    {
        if (siteId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(SiteId), "El punto de acceso debe pertenecer a un sitio.");
        }

        var point = new AccessPoint(Guid.CreateVersion7(now), siteId, now);
        point.Update(name, type, to, from, failMode, now);
        return point;
    }

    public void Update(string name, AccessPointType type, Space to, Space? from, FailMode failMode, DateTimeOffset now)
    {
        var validName = Space.RequiredName(name);
        if (!Enum.IsDefined(type))
        {
            throw new DomainValidationException(nameof(Type), "Tipo de punto de acceso inválido.");
        }

        if (!Enum.IsDefined(failMode))
        {
            throw new DomainValidationException(nameof(FailMode), "Modo de fallo inválido.");
        }

        if (to.SiteId != SiteId)
        {
            throw new DomainValidationException(nameof(ToSpaceId), "El espacio no existe en el sitio.");
        }

        if (from is not null && from.SiteId != SiteId)
        {
            throw new DomainValidationException(nameof(FromSpaceId), "El espacio no existe en el sitio.");
        }

        if (from is not null && from.Id == to.Id)
        {
            throw new DomainValidationException(nameof(FromSpaceId), "El espacio de origen no puede ser el mismo al que da acceso.");
        }

        Name = validName;
        Type = type;
        ToSpaceId = to.Id;
        FromSpaceId = from?.Id;
        FailMode = failMode;
        UpdatedAt = now;
    }
}
