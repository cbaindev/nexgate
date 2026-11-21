using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nexgate.Host.Tests;

public class ErrorTests
{
    [Fact]
    public async Task Unhandled_exception_returns_500_problem_details_with_correlation_and_no_internals()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(TestEndpoints.Fail);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("internal_error", json.RootElement.GetProperty("code").GetString());

        var correlationHeader = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
        Assert.Equal(correlationHeader, json.RootElement.GetProperty("correlationId").GetString());

        Assert.DoesNotContain(TestEndpoints.SecretFailureDetail, body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("   at ", body);
        Assert.DoesNotContain("TestEndpoints", body);
    }

    [Fact]
    public async Task Invalid_request_returns_400_listing_each_invalid_field()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var response = await client.PostAsJsonAsync("/api/v1/sites",
            new { name = new string('x', 250), type = "castle", timeZone = "America/Cordoba" });
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_error", json.RootElement.GetProperty("code").GetString());
        var errors = json.RootElement.GetProperty("errors");
        Assert.Equal(1, errors.GetProperty("Name").GetArrayLength());
        Assert.Equal(1, errors.GetProperty("Type").GetArrayLength());
        Assert.False(errors.TryGetProperty("TimeZone", out _), "America/Cordoba es un alias IANA válido");
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task Missing_required_fields_return_400()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var response = await client.PostAsJsonAsync("/api/v1/sites", new { });
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = json.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("Type", out _));
        Assert.True(errors.TryGetProperty("TimeZone", out _));
    }

    [Fact]
    public async Task Valid_request_is_processed()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var response = await client.PostAsJsonAsync("/api/v1/sites", new { name = "Ana", type = "house", timeZone = "UTC" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
