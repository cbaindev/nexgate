using System.Net;

namespace Nexgate.Host.Tests;

public class HealthTests
{
    [Fact]
    public async Task Live_responds_200_without_checking_dependencies()
    {
        // Aun con la base de datos caída, el proceso está vivo.
        using var factory = new NexgateFactory { ConnectionString = NexgateFactory.UnreachableDatabase };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_responds_200_when_the_database_accepts_connections()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_responds_503_without_internal_details_when_the_database_is_down()
    {
        using var factory = new NexgateFactory { ConnectionString = NexgateFactory.UnreachableDatabase };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("super-secreto-123", body);
        Assert.DoesNotContain("127.0.0.1", body);
        Assert.DoesNotContain("Npgsql", body);
    }

    [Fact]
    public async Task Health_endpoints_do_not_require_authentication_and_return_no_business_data()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        foreach (var path in new[] { "/health/live", "/health/ready" })
        {
            var body = await client.GetStringAsync(path);
            Assert.Equal("Healthy", body);
        }
    }
}
