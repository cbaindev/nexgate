using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nexgate.Host.Tests;

public class TenancyApiTests
{
    private const string Cordoba = "America/Argentina/Cordoba";

    internal static async Task<JsonElement> CreateSiteAsync(HttpClient client, string name, string type = "building")
    {
        var response = await client.PostAsJsonAsync("/api/v1/sites", new { name, type, timeZone = Cordoba });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> ProblemOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    // --- Organizaciones ---

    [Fact]
    public async Task Platform_operator_creates_an_active_organization_with_an_administrator_invitation()
    {
        using var factory = new NexgateFactory();
        using var client = await factory.CreateOperatorClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/platform/organizations",
            new { name = "  Consorcio Norte  ", administratorEmail = "Admin.Norte@Example.com" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
        Assert.Equal("Consorcio Norte", body.GetProperty("name").GetString());
        Assert.Equal("active", body.GetProperty("status").GetString());
        var invitation = body.GetProperty("administratorInvitation");
        Assert.Equal("admin.norte@example.com", invitation.GetProperty("email").GetString());
        Assert.Equal("administrator", invitation.GetProperty("role").GetString());
        Assert.Equal("organization", invitation.GetProperty("scopeType").GetString());
        Assert.False(string.IsNullOrEmpty(invitation.GetProperty("token").GetString()));
        Assert.True(invitation.GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Organization_with_an_invalid_name_is_rejected(string? name)
    {
        using var factory = new NexgateFactory();
        using var client = await factory.CreateOperatorClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/platform/organizations", new { name, administratorEmail = NexgateFactory.NewEmail() });
        var problem = await ProblemOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Name", out _));
    }

    [Fact]
    public async Task Organization_name_longer_than_200_characters_is_rejected()
    {
        using var factory = new NexgateFactory();
        using var client = await factory.CreateOperatorClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/platform/organizations",
            new { name = new string('a', 201), administratorEmail = NexgateFactory.NewEmail() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ProblemOf(response)).GetProperty("errors").TryGetProperty("Name", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("sin-arroba")]
    [InlineData("con espacio@example.com")]
    public async Task Organization_with_an_invalid_administrator_email_is_rejected_and_not_created(string? email)
    {
        using var factory = new NexgateFactory();
        using var client = await factory.CreateOperatorClientAsync();
        var name = $"Sin admin {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/v1/platform/organizations", new { name, administratorEmail = email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ProblemOf(response)).GetProperty("errors").TryGetProperty("AdministratorEmail", out _));
        await using var connection = new Npgsql.NpgsqlConnection((await TestDatabase.GetAsync()).OwnerConnectionString);
        await connection.OpenAsync();
        await using var count = new Npgsql.NpgsqlCommand("SELECT count(*) FROM tenancy.organization WHERE name = @name", connection);
        count.Parameters.AddWithValue("name", name);
        Assert.Equal(0L, await count.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Without_platform_access_organizations_cannot_be_created()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var response = await admin.PostAsJsonAsync("/api/v1/platform/organizations",
            new { name = "Intruso", administratorEmail = NexgateFactory.NewEmail() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("permission_denied", (await ProblemOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Anonymous_callers_cannot_create_organizations()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/platform/organizations",
            new { name = "Intruso", administratorEmail = NexgateFactory.NewEmail() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("authentication_required", (await ProblemOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Caller_reads_its_active_organization()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync("Torre Sur");
        using var client = factory.CreateClientFor(organization);

        var response = await client.GetAsync("/api/v1/organization");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(organization, body.GetProperty("id").GetGuid());
        Assert.Equal("Torre Sur", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Active_organization_that_does_not_exist_responds_404()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var client = factory.CreateClientFor(organization);

        // La membresía sigue, pero la organización ya no es visible (se borra como owner, fuera de la API).
        await using (var connection = new Npgsql.NpgsqlConnection((await TestDatabase.GetAsync()).OwnerConnectionString))
        {
            await connection.OpenAsync();
            // FORCE aplica RLS también al owner: se suspende solo dentro de esta transacción.
            await using var transaction = await connection.BeginTransactionAsync();
            await using var delete = new Npgsql.NpgsqlCommand(
                "ALTER TABLE tenancy.organization NO FORCE ROW LEVEL SECURITY; " +
                "DELETE FROM tenancy.organization WHERE id = @id; " +
                "ALTER TABLE tenancy.organization FORCE ROW LEVEL SECURITY;", connection, transaction);
            delete.Parameters.AddWithValue("id", organization);
            await delete.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }

        var response = await client.GetAsync("/api/v1/organization");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Sitios ---

    [Fact]
    public async Task Site_is_created_in_the_active_organization()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var client = factory.CreateClientFor(organization);

        var response = await client.PostAsJsonAsync("/api/v1/sites", new
        {
            name = "Edificio Alvear",
            type = "building",
            timeZone = Cordoba,
            configuration = new { visitorsRequireApproval = true }
        });
        var site = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/v1/sites/{site.GetProperty("id").GetGuid()}", response.Headers.Location?.OriginalString);
        Assert.Equal(organization, site.GetProperty("organizationId").GetGuid());
        Assert.Equal("Edificio Alvear", site.GetProperty("name").GetString());
        Assert.Equal("building", site.GetProperty("type").GetString());
        Assert.Equal(Cordoba, site.GetProperty("timeZone").GetString());
        Assert.True(site.GetProperty("configuration").GetProperty("visitorsRequireApproval").GetBoolean());
    }

    [Fact]
    public async Task Site_with_an_unknown_time_zone_is_rejected()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var response = await client.PostAsJsonAsync("/api/v1/sites", new { name = "X", type = "house", timeZone = "America/Cordoba_Inexistente" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ProblemOf(response)).GetProperty("errors").TryGetProperty("TimeZone", out _));
    }

    [Fact]
    public async Task Site_with_an_unknown_type_is_rejected()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var response = await client.PostAsJsonAsync("/api/v1/sites", new { name = "X", type = "castle", timeZone = Cordoba });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ProblemOf(response)).GetProperty("errors").TryGetProperty("Type", out _));
    }

    [Fact]
    public async Task Configuration_that_is_not_an_object_is_rejected()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var response = await client.PostAsJsonAsync("/api/v1/sites", new { name = "X", type = "house", timeZone = Cordoba, configuration = new[] { 1, 2 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ProblemOf(response)).GetProperty("errors").TryGetProperty("Configuration", out _));
    }

    [Fact]
    public async Task Organization_named_in_the_request_body_is_ignored()
    {
        using var factory = new NexgateFactory();
        var mine = await factory.CreateOrganizationAsync("Mía");
        var other = await factory.CreateOrganizationAsync("Ajena");
        using var client = factory.CreateClientFor(mine);

        var response = await client.PostAsJsonAsync("/api/v1/sites", new { name = "Infiltrado", type = "company", timeZone = Cordoba, organizationId = other });
        var site = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(mine, site.GetProperty("organizationId").GetGuid());

        using var otherClient = factory.CreateClientFor(other);
        Assert.Empty(await otherClient.GetFromJsonAsync<JsonElement[]>("/api/v1/sites") ?? []);
    }

    [Fact]
    public async Task Duplicate_site_name_in_the_same_organization_responds_409()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var client = factory.CreateClientFor(organization);
        await CreateSiteAsync(client, "Torre A");

        var response = await client.PostAsJsonAsync("/api/v1/sites", new { name = "TORRE a", type = "building", timeZone = Cordoba });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("site_name_conflict", (await ProblemOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Same_site_name_is_allowed_in_different_organizations()
    {
        using var factory = new NexgateFactory();
        using var first = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        using var second = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        await CreateSiteAsync(first, "Edificio Central");
        await CreateSiteAsync(second, "Edificio Central");
    }

    [Fact]
    public async Task Sites_are_listed_by_name_and_only_for_the_active_organization()
    {
        using var factory = new NexgateFactory();
        using var mine = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        await CreateSiteAsync(mine, "Zeta");
        await CreateSiteAsync(mine, "Alfa");
        await CreateSiteAsync(other, "Ajeno");

        var sites = await mine.GetFromJsonAsync<JsonElement[]>("/api/v1/sites");

        Assert.Equal(["Alfa", "Zeta"], sites!.Select(s => s.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Site_of_the_active_organization_is_returned()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var created = await CreateSiteAsync(client, "Casa Quinta", "house");

        var site = await client.GetFromJsonAsync<JsonElement>($"/api/v1/sites/{created.GetProperty("id").GetGuid()}");

        Assert.Equal("Casa Quinta", site.GetProperty("name").GetString());
        Assert.Equal("house", site.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Site_of_another_organization_responds_404()
    {
        using var factory = new NexgateFactory();
        using var owner = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        using var intruder = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await CreateSiteAsync(owner, "Privado");

        var response = await intruder.GetAsync($"/api/v1/sites/{site.GetProperty("id").GetGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Site_is_renamed_and_reconfigured()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await CreateSiteAsync(client, "Nombre viejo");
        var id = site.GetProperty("id").GetGuid();

        var response = await client.PutAsJsonAsync($"/api/v1/sites/{id}", new
        {
            name = "Nombre nuevo",
            timeZone = "America/Argentina/Buenos_Aires",
            configuration = new { guardOnDuty = true }
        });
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Nombre nuevo", updated.GetProperty("name").GetString());
        Assert.Equal("America/Argentina/Buenos_Aires", updated.GetProperty("timeZone").GetString());
        Assert.True(updated.GetProperty("configuration").GetProperty("guardOnDuty").GetBoolean());
        Assert.Equal(site.GetProperty("organizationId").GetGuid(), updated.GetProperty("organizationId").GetGuid());
    }

    [Fact]
    public async Task Site_of_another_organization_cannot_be_modified()
    {
        using var factory = new NexgateFactory();
        using var owner = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        using var intruder = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await CreateSiteAsync(owner, "Intacto");
        var id = site.GetProperty("id").GetGuid();

        var response = await intruder.PutAsJsonAsync($"/api/v1/sites/{id}", new { name = "Pisado", timeZone = Cordoba });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var unchanged = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/sites/{id}");
        Assert.Equal("Intacto", unchanged.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("GET", "/api/v1/sites")]
    [InlineData("GET", "/api/v1/sites/0198a3d4-0000-7000-8000-000000000000")]
    [InlineData("POST", "/api/v1/sites")]
    [InlineData("PUT", "/api/v1/sites/0198a3d4-0000-7000-8000-000000000000")]
    [InlineData("GET", "/api/v1/organization")]
    public async Task Organization_operations_without_an_active_organization_respond_403(string method, string path)
    {
        using var factory = new NexgateFactory();
        // El operador de plataforma no tiene membresías: su token no tiene organización activa.
        using var client = await factory.CreateOperatorClientAsync();

        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { name = "X", type = "house", timeZone = Cordoba });
        }

        var response = await client.SendAsync(request);
        var problem = await ProblemOf(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("organization_context_required", problem.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("GET", "/api/v1/sites")]
    [InlineData("POST", "/api/v1/sites")]
    [InlineData("GET", "/api/v1/organization")]
    public async Task Organization_operations_without_a_token_respond_401(string method, string path)
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST")
        {
            request.Content = JsonContent.Create(new { name = "X", type = "house", timeZone = Cordoba });
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("authentication_required", (await ProblemOf(response)).GetProperty("code").GetString());
    }
}
