using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Api.Rest.Controllers.Tenancy;

[Tags("Organization")]
[Route(Prefix + "organization")]
public sealed class OrganizationController(IOrganizationService organizations) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.OrganizationRead)]
    [EndpointName("getOrganization")]
    [EndpointSummary("Organización activa")]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(CancellationToken ct) =>
        TenancyResults.ToHttp(await organizations.GetActiveAsync(ct), Results.Ok);
}
