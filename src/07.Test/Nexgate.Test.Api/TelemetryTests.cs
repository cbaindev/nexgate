using System.Net.Http.Json;

namespace Nexgate.Test.Api;

public class TelemetryTests
{
    [Fact]
    public async Task Logs_of_a_request_with_an_active_organization_carry_its_identifier()
    {
        var organization = Guid.NewGuid();
        using var factory = new NexgateFactory { Organization = organization };
        using var client = factory.CreateClient();

        await client.GetAsync("/api/v1/sample/ping");

        var log = Assert.Single(factory.Logs.Entries, e => e.Message == "Ping recibido");
        Assert.Equal(organization.ToString(), log.Scope["OrganizationId"]);
    }

    [Fact]
    public async Task A_request_without_an_active_organization_is_logged_without_an_organization_identifier()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/sample/ping");

        Assert.True(response.IsSuccessStatusCode);
        var log = Assert.Single(factory.Logs.Entries, e => e.Message == "Ping recibido");
        Assert.False(log.Scope.ContainsKey("OrganizationId"));
    }

    [Fact]
    public async Task Health_checks_run_without_an_organization_and_without_errors()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.True(response.IsSuccessStatusCode);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Level >= Microsoft.Extensions.Logging.LogLevel.Error);
        Assert.All(factory.Logs.Entries, e => Assert.False(e.Scope.ContainsKey("OrganizationId")));
    }

    [Fact]
    public async Task Personal_data_sent_in_a_request_is_not_written_to_the_logs()
    {
        using var factory = new NexgateFactory { Organization = Guid.NewGuid() };
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/sample/greet", new { name = "Juan Perez 30123456" });
        await client.PostAsJsonAsync("/api/v1/sample/greet", new { name = new string('x', 80) + "30123456" });

        Assert.NotEmpty(factory.Logs.Entries);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.AllText.Contains("30123456") || e.AllText.Contains("Juan"));
    }
}
