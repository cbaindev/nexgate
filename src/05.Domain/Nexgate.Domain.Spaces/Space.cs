using Nexgate.Service.Spaces.Contracts;

namespace Nexgate.Domain.Spaces;

/// <summary>Una regla del dominio no se cumple. El campo usa el nombre de la propiedad pública de la solicitud.</summary>
public sealed class DomainValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

/// <summary>
/// Nodo del árbol físico de un sitio (D-06). La raíz es de tipo sitio y no tiene padre; todo otro espacio tiene un
/// padre del mismo sitio. <see cref="Path"/> es la secuencia de identificadores desde la raíz (formato <c>ltree</c>) y
/// no cambia: no se mueven espacios.
/// </summary>
public sealed class Space
{
    // Para la materialización desde la base.
    private Space()
    {
        Name = Path = string.Empty;
    }

    private Space(Guid id, Guid siteId, Guid? parentId, string path, SpaceType type, string name, bool isCommon, DateTimeOffset now)
    {
        Id = id;
        SiteId = siteId;
        ParentId = parentId;
        Path = path;
        Type = type;
        Name = name;
        IsCommon = isCommon;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    /// <summary>Organización dueña. La asigna la persistencia con la organización activa y no cambia nunca.</summary>
    public Guid OrganizationId { get; private set; }

    public Guid SiteId { get; private set; }

    public Guid? ParentId { get; private set; }

    public string Path { get; private set; }

    public SpaceType Type { get; private set; }

    public string Name { get; private set; }

    public bool IsCommon { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsRoot => ParentId is null;

    /// <summary>Niveles debajo de la raíz (la raíz tiene 0).</summary>
    public int Depth => Path.Count(c => c == '.');

    /// <summary>Identificadores desde la raíz hasta este espacio inclusive.</summary>
    public IReadOnlyList<Guid> PathIds => Path.Split('.').Select(label => Guid.ParseExact(label, "N")).ToList();

    /// <summary>Crea un espacio. Sin <paramref name="parent"/> es la raíz del sitio y tiene que ser de tipo sitio.</summary>
    public static Space Create(Guid siteId, Space? parent, SpaceType type, string name, bool isCommon, DateTimeOffset now)
    {
        if (siteId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(SiteId), "El espacio debe pertenecer a un sitio.");
        }

        CheckType(type, isRoot: parent is null);
        var id = Guid.CreateVersion7(now);
        var label = Label(id);
        string path;
        if (parent is null)
        {
            path = label;
        }
        else
        {
            if (parent.SiteId != siteId)
            {
                throw new DomainValidationException(nameof(ParentId), "El padre no existe en el sitio.");
            }

            if (parent.Depth + 1 > SpaceLimits.MaxDepth)
            {
                throw new DomainValidationException(nameof(ParentId), $"El árbol no puede tener más de {SpaceLimits.MaxDepth} niveles debajo de la raíz.");
            }

            path = parent.Path + "." + label;
        }

        return new Space(id, siteId, parent?.Id, path, type, RequiredName(name), isCommon, now);
    }

    public void Update(string name, SpaceType type, bool isCommon, DateTimeOffset now)
    {
        CheckType(type, IsRoot);
        Name = RequiredName(name);
        Type = type;
        IsCommon = isCommon;
        UpdatedAt = now;
    }

    /// <summary>Etiqueta <c>ltree</c> de un espacio: su identificador en hexadecimal sin guiones.</summary>
    public static string Label(Guid id) => id.ToString("N");

    /// <summary>
    /// Orden de árbol: cada espacio después de su padre y los hermanos por nombre (sin distinguir mayúsculas).
    /// </summary>
    public static IReadOnlyList<Space> InTreeOrder(IEnumerable<Space> spaces)
    {
        var all = spaces.ToList();
        var children = all.ToLookup(s => s.ParentId);
        var ordered = new List<Space>(all.Count);

        void Visit(Guid? parentId)
        {
            foreach (var child in children[parentId]
                         .OrderBy(s => s.Name, StringComparer.InvariantCultureIgnoreCase)
                         .ThenBy(s => s.Id))
            {
                ordered.Add(child);
                Visit(child.Id);
            }
        }

        Visit(null);
        return ordered;
    }

    private static void CheckType(SpaceType type, bool isRoot)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainValidationException(nameof(Type), "Tipo de espacio inválido.");
        }

        if (isRoot && type != SpaceType.Site)
        {
            throw new DomainValidationException(nameof(Type), "La raíz del sitio debe ser de tipo site.");
        }

        if (!isRoot && type == SpaceType.Site)
        {
            throw new DomainValidationException(nameof(Type), "Solo la raíz del sitio puede ser de tipo site.");
        }
    }

    internal static string RequiredName(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Name), "El nombre es obligatorio.");
        }

        if (trimmed.Length > SpaceLimits.NameMaxLength)
        {
            throw new DomainValidationException(nameof(Name), $"El nombre no puede superar {SpaceLimits.NameMaxLength} caracteres.");
        }

        return trimmed;
    }
}
