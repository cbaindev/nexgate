using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nexgate.Test.Api;

public class ErrorTests
{
    [Fact]
    public async Task Unhandled_exception_returns_500_problem_details_with_correlation_and_no_internals()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/sample/fail");
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("internal_error", json.RootElement.GetProperty("code").GetString());

        var correlationHeader = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
        Assert.Equal(correlationHeader, json.RootElement.GetProperty("correlationId").GetString());

        Assert.DoesNotContain("detalle-interno-secreto", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("   at ", body);
        Assert.DoesNotContain("SampleModule", body);
    }

    [Fact]
    public async Task Invalid_request_returns_400_listing_each_invalid_field()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/sample/greet", new { name = new string('x', 80) });
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_error", json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Name", out var nameErrors));
        Assert.Equal(1, nameErrors.GetArrayLength());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task Missing_required_field_returns_400()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/sample/greet", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Valid_request_is_processed()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/sample/greet", new { name = "Ana" });
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hola, Ana", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Failure_endpoint_is_not_found_when_disabled()
    {
        using var factory = new NexgateFactory { EnableFailureEndpoint = false };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/sample/fail");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not_found", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Validation_error_keeps_the_spanish_title()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/sample/greet", new { });
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("Error de validación", json.RootElement.GetProperty("title").GetString());
        Assert.Equal("El nombre es obligatorio", json.RootElement.GetProperty("errors").GetProperty("Name")[0].GetString());
    }
}
