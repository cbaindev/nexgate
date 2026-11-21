using System.Net;
using System.Text.Json;

namespace Nexgate.Host.Tests;

public class VersioningTests
{
    [Fact]
    public async Task Business_endpoint_under_the_version_prefix_is_served()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var response = await client.GetAsync("/api/v1/sites");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Business_endpoint_without_the_version_prefix_returns_404_as_problem_details()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var response = await client.GetAsync("/sites");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("not_found", json.RootElement.GetProperty("code").GetString());
    }
}
