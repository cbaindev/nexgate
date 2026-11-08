namespace Nexgate.BuildingBlocks.Tenancy;

/// <summary>Organización activa de la solicitud o del alcance actual, si la hay.</summary>
public interface ICurrentOrganization
{
    Guid? Id { get; }
}

/// <summary>Para procesos sin solicitudes HTTP (workers) y para el migrador.</summary>
public sealed class NullCurrentOrganization : ICurrentOrganization
{
    public Guid? Id => null;
}
