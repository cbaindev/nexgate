namespace Nexgate.Common.BuildingBlocks.Tenancy;

/// <summary>Organización activa de la solicitud actual, si la hay. Lo completa el módulo de Tenancy.</summary>
public interface ICurrentOrganization
{
    Guid? Id { get; }
}

public static class OrganizationContextKeys
{
    public const string OrganizationId = "nexgate.organization-id";
}

/// <summary>Para procesos sin solicitudes HTTP (workers).</summary>
public sealed class NullCurrentOrganization : ICurrentOrganization
{
    public Guid? Id => null;
}
