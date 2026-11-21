namespace Nexgate.Modules.Tenancy.Domain;

public enum OrganizationStatus
{
    Active
}

/// <summary>Cuenta que contrata el servicio. Es el límite de aislamiento de los datos (D-02).</summary>
public sealed class Organization
{
    public const int NameMaxLength = 200;

    private Organization(Guid id, string name, OrganizationStatus status, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Status = status;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public OrganizationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Organization Create(string name, DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), DomainRules.RequiredText(name, NameMaxLength, nameof(Name)), OrganizationStatus.Active, now);
}
