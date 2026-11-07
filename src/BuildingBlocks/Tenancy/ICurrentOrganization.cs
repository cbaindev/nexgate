using Microsoft.AspNetCore.Http;

namespace Nexgate.BuildingBlocks.Tenancy;

/// <summary>Organización activa de la solicitud actual, si la hay. Lo completa el módulo de Tenancy.</summary>
public interface ICurrentOrganization
{
    Guid? Id { get; }
}

public static class OrganizationContextKeys
{
    public const string OrganizationId = "nexgate.organization-id";
}

/// <summary>Lee la organización activa desde el contexto HTTP (la fija el middleware de tenancy cuando exista).</summary>
public sealed class HttpContextCurrentOrganization(IHttpContextAccessor accessor) : ICurrentOrganization
{
    public Guid? Id =>
        accessor.HttpContext?.Items.TryGetValue(OrganizationContextKeys.OrganizationId, out var value) == true
            ? value as Guid?
            : null;
}

/// <summary>Para procesos sin solicitudes HTTP (workers).</summary>
public sealed class NullCurrentOrganization : ICurrentOrganization
{
    public Guid? Id => null;
}
