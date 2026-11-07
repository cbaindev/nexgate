namespace Nexgate.Host.Tests;

public class CorrelationTests
{
    private static HttpRequestMessage Ping(string? correlationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, TestEndpoints.Ping);
        if (correlationId is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        }

        return request;
    }

    [Fact]
    public async Task A_valid_client_correlation_id_is_reused_in_the_response_and_the_logs()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(Ping("cliente-abc_123.xyz"));

        Assert.Equal("cliente-abc_123.xyz", Assert.Single(response.Headers.GetValues("X-Correlation-Id")));
        var log = Assert.Single(factory.Logs.Entries, e => e.Message == TestEndpoints.PingLogMessage);
        Assert.Equal("cliente-abc_123.xyz", log.Scope["CorrelationId"]);
    }

    [Fact]
    public async Task A_missing_correlation_id_is_generated_and_returned()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(Ping(null));

        var generated = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
        Assert.Matches("^[0-9a-f]{32}$", generated);
        var log = Assert.Single(factory.Logs.Entries, e => e.Message == TestEndpoints.PingLogMessage);
        Assert.Equal(generated, log.Scope["CorrelationId"]);
    }

    [Theory]
    [InlineData("con espacios y <script>")]
    [InlineData("a/b\\c")]
    public async Task A_malformed_correlation_id_is_ignored_and_replaced(string malformed)
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(Ping(malformed));

        var returned = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
        Assert.NotEqual(malformed, returned);
        Assert.Matches("^[0-9a-f]{32}$", returned);
    }

    [Fact]
    public async Task A_correlation_id_over_the_length_limit_is_ignored_and_replaced()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();
        var tooLong = new string('a', 65);

        var response = await client.SendAsync(Ping(tooLong));

        var returned = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
        Assert.NotEqual(tooLong, returned);
    }

    [Fact]
    public async Task A_correlation_id_at_the_length_limit_is_accepted()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();
        var atLimit = new string('a', 64);

        var response = await client.SendAsync(Ping(atLimit));

        Assert.Equal(atLimit, Assert.Single(response.Headers.GetValues("X-Correlation-Id")));
    }
}
