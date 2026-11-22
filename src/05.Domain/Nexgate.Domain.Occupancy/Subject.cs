using Nexgate.Service.Occupancy.Contracts;

namespace Nexgate.Domain.Occupancy;

/// <summary>Una regla del dominio no se cumple. El campo usa el nombre de la propiedad pública de la solicitud.</summary>
public sealed class DomainValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

public enum SubjectKind
{
    Person,
    Anonymous
}

public static class SubjectKinds
{
    public static bool TryParse(string? value, out SubjectKind kind)
    {
        (var ok, kind) = value switch
        {
            SubjectKindNames.Person => (true, SubjectKind.Person),
            SubjectKindNames.Anonymous => (true, SubjectKind.Anonymous),
            _ => (false, default)
        };
        return ok;
    }

    public static string ToName(SubjectKind kind) => kind switch
    {
        SubjectKind.Person => SubjectKindNames.Person,
        SubjectKind.Anonymous => SubjectKindNames.Anonymous,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

/// <summary>
/// Representación local de alguien en un sitio (docs/02). Registro mínimo de add-access-evaluator: sin datos personales
/// ni vínculo con el padrón, que llegan con add-people-registry.
/// </summary>
public sealed class Subject
{
    private Subject()
    {
        Alias = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Organización dueña. La asigna la persistencia con la organización activa y no cambia nunca.</summary>
    public Guid OrganizationId { get; private set; }

    public Guid SiteId { get; private set; }

    public SubjectKind Kind { get; private set; }

    public string Alias { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Subject Create(Guid siteId, SubjectKind kind, string alias, Guid createdBy, DateTimeOffset now)
    {
        if (siteId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(SiteId), "El sujeto debe pertenecer a un sitio.");
        }

        var subject = new Subject { Id = Guid.CreateVersion7(now), SiteId = siteId, CreatedBy = createdBy, CreatedAt = now };
        subject.Update(kind, alias, now);
        return subject;
    }

    public void Update(SubjectKind kind, string alias, DateTimeOffset now)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainValidationException(nameof(Kind), "Tipo de sujeto inválido.");
        }

        var trimmed = alias?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(nameof(Alias), "El alias es obligatorio.");
        }

        if (trimmed.Length > SubjectLimits.AliasMaxLength)
        {
            throw new DomainValidationException(nameof(Alias), $"El alias no puede superar {SubjectLimits.AliasMaxLength} caracteres.");
        }

        Kind = kind;
        Alias = trimmed;
        UpdatedAt = now;
    }
}
