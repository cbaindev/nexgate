using Nexgate.Common.BuildingBlocks.Tenancy;

namespace Nexgate.Api.Rest.Tenancy;

/// <summary>Lee la organización activa desde el contexto HTTP (la fija el middleware de tenancy cuando exista).</summary>
public sealed class HttpContextCurrentOrganization(IHttpContextAccessor accessor) : ICurrentOrganization
{
    public Guid? Id =>
        accessor.HttpContext?.Items.TryGetValue(OrganizationContextKeys.OrganizationId, out var value) == true
            ? value as Guid?
            : null;
}
