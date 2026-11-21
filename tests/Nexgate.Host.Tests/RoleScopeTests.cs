using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Nexgate.Host.Tests;

/// <summary>Escenarios de <c>role-scopes</c> y de <c>Site operations authorized by role scope</c>.</summary>
public class RoleScopeTests
{
    private const string Cordoba = "America/Argentina/Cordoba";

    private static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> SiteAsync(HttpClient admin, string name) =>
        (await TenancyApiTests.CreateSiteAsync(admin, name)).GetProperty("id").GetGuid();

    private static async Task<Dictionary<string, JsonElement>> PermissionsAsync(NexgateFactory factory, string accessToken)
    {
        using var client = factory.CreateClientWithToken(accessToken);
        var body = await Json(await client.GetAsync("/api/v1/me/permissions"));
        return body.GetProperty("permissions").EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!, p => p);
    }

    // --- Asignación y revocación ---

    [Fact]
    public async Task Role_with_site_scope_is_assigned()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var site = await SiteAsync(admin, "Torre A");
        var resident = await factory.CreateUserAsync(organization, "resident");

        var response = await admin.PostAsJsonAsync($"/api/v1/members/{resident.UserId}/role-assignments",
            new { role = "guard", scopeType = "site", siteId = site, validUntil = DateTimeOffset.UtcNow.AddDays(30) });
        var body = await Json(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("guard", body.GetProperty("role").GetString());
        Assert.Equal("site", body.GetProperty("scopeType").GetString());
        Assert.Equal(site, body.GetProperty("siteId").GetGuid());
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Scope_outside_the_active_organization_is_rejected_like_a_missing_site()
    {
        using var factory = new NexgateFactory();
        var other = await factory.CreateOrganizationAsync();
        using var otherAdmin = factory.CreateClientFor(other);
        var foreignSite = await SiteAsync(otherAdmin, "Ajeno");
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var member = await factory.CreateUserAsync(organization, "resident");

        var foreign = await admin.PostAsJsonAsync($"/api/v1/members/{member.UserId}/role-assignments",
            new { role = "guard", scopeType = "site", siteId = foreignSite });
        var missing = await admin.PostAsJsonAsync($"/api/v1/members/{member.UserId}/role-assignments",
            new { role = "guard", scopeType = "site", siteId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal((await Json(missing)).GetProperty("errors").ToString(), (await Json(foreign)).GetProperty("errors").ToString());
    }

    [Fact]
    public async Task Role_cannot_be_assigned_to_a_non_member()
    {
        using var factory = new NexgateFactory();
        var outsider = factory.Administrator(await factory.CreateOrganizationAsync());
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var response = await admin.PostAsJsonAsync($"/api/v1/members/{outsider.UserId}/role-assignments",
            new { role = "guard", scopeType = "organization" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Validity_that_ends_before_it_starts_is_rejected()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var member = await factory.CreateUserAsync(organization, "resident");
        var now = DateTimeOffset.UtcNow;

        var response = await admin.PostAsJsonAsync($"/api/v1/members/{member.UserId}/role-assignments",
            new { role = "guard", scopeType = "organization", validFrom = now.AddDays(2), validUntil = now.AddDays(1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("ValidUntil", out _));
    }

    [Fact]
    public async Task Revoked_assignment_stops_applying_on_the_next_request()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var guard = await factory.CreateUserAsync(organization, "guard");
        using var guardClient = factory.CreateClientWithToken(guard.Tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await guardClient.GetAsync("/api/v1/sites")).StatusCode);
        var members = await Json(await admin.GetAsync("/api/v1/members"));
        var assignment = members.EnumerateArray().Single(m => m.GetProperty("userId").GetGuid() == guard.UserId)
            .GetProperty("roleAssignments").EnumerateArray().Single().GetProperty("id").GetGuid();

        var revoked = await admin.DeleteAsync($"/api/v1/members/{guard.UserId}/role-assignments/{assignment}");
        var after = await guardClient.GetAsync("/api/v1/sites");

        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        Assert.Equal("permission_denied", (await Json(after)).GetProperty("code").GetString());
    }

    // --- Permisos efectivos ---

    [Fact]
    public async Task Permissions_follow_the_role_catalog()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();

        var admin = await PermissionsAsync(factory, factory.Administrator(organization).Tokens.AccessToken);
        Assert.Equal(["members.manage", "members.read", "organization.read", "sites.manage", "sites.read"], admin.Keys.Order().ToArray());
        Assert.All(admin.Values, p => Assert.True(p.GetProperty("organization").GetBoolean()));

        foreach (var role in new[] { "guard", "resident", "employee" })
        {
            var user = await factory.CreateUserAsync(organization, role);
            var permissions = await PermissionsAsync(factory, user.Tokens.AccessToken);
            Assert.Equal(["organization.read", "sites.read"], permissions.Keys.Order().ToArray());
        }
    }

    [Fact]
    public async Task Assignment_outside_its_validity_grants_nothing()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var member = await factory.CreateUserAsync(organization, "resident");
        var now = DateTimeOffset.UtcNow;
        await admin.PostAsJsonAsync($"/api/v1/members/{member.UserId}/role-assignments",
            new { role = "administrator", scopeType = "organization", validFrom = now.AddDays(1) });
        await admin.PostAsJsonAsync($"/api/v1/members/{member.UserId}/role-assignments",
            new { role = "administrator", scopeType = "organization", validFrom = now.AddDays(-2), validUntil = now.AddDays(-1) });

        var permissions = await PermissionsAsync(factory, member.Tokens.AccessToken);

        Assert.DoesNotContain("members.manage", permissions.Keys);
        Assert.DoesNotContain("sites.manage", permissions.Keys);
    }

    [Fact]
    public async Task Only_assignments_of_the_active_organization_count()
    {
        using var factory = new NexgateFactory();
        var first = await factory.CreateOrganizationAsync();
        var second = await factory.CreateOrganizationAsync();
        // Administrador de la primera, residente en la segunda.
        var admin = factory.Administrator(first);
        var invitation = await factory.InviteAsync(second, admin.Email, "resident");
        using (var client = factory.CreateClientWithToken(admin.Tokens.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK,
                (await client.PostAsJsonAsync("/api/v1/invitations/accept", new { token = invitation.Token })).StatusCode);
        }

        var inSecond = await factory.SignInAsync(admin.Email, admin.Password, second);
        var permissions = await PermissionsAsync(factory, inSecond.AccessToken);
        using var secondClient = factory.CreateClientWithToken(inSecond.AccessToken);

        Assert.Equal(["organization.read", "sites.read"], permissions.Keys.Order().ToArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await secondClient.GetAsync("/api/v1/members")).StatusCode);
    }

    [Fact]
    public async Task Permissions_without_an_active_organization_are_empty()
    {
        using var factory = new NexgateFactory();
        var operatorUser = await factory.OperatorAsync();
        using var client = factory.CreateClientWithToken(operatorUser.Tokens.AccessToken);

        var body = await Json(await client.GetAsync("/api/v1/me/permissions"));

        Assert.Equal(JsonValueKind.Null, body.GetProperty("organizationId").ValueKind);
        Assert.Empty(body.GetProperty("permissions").EnumerateArray());
    }

    // --- Último administrador ---

    [Fact]
    public async Task The_last_administrator_assignment_cannot_be_revoked()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        var admin = factory.Administrator(organization);
        using var client = factory.CreateClientFor(organization);
        var assignment = await AdministratorAssignmentAsync(client, admin.UserId);

        var response = await client.DeleteAsync($"/api/v1/members/{admin.UserId}/role-assignments/{assignment}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("last_administrator", (await Json(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Two_concurrent_revocations_never_leave_the_organization_without_an_administrator()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        var first = factory.Administrator(organization);
        var second = await factory.CreateUserAsync(organization, "administrator");
        using var client = factory.CreateClientFor(organization);
        var firstAssignment = await AdministratorAssignmentAsync(client, first.UserId);
        var secondAssignment = await AdministratorAssignmentAsync(client, second.UserId);
        using var secondClient = factory.CreateClientWithToken(second.Tokens.AccessToken);

        var responses = await Task.WhenAll(
            client.DeleteAsync($"/api/v1/members/{second.UserId}/role-assignments/{secondAssignment}"),
            secondClient.DeleteAsync($"/api/v1/members/{first.UserId}/role-assignments/{firstAssignment}"));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var db = await TestDatabase.GetAsync();
        await using var connection = await db.OpenAsAppAsync(organization);
        await using var count = new Npgsql.NpgsqlCommand(
            "SELECT count(*) FROM identity.role_assignment WHERE role = 'administrator' AND scope_type = 'organization'", connection);
        Assert.Equal(1L, await count.ExecuteScalarAsync());
    }

    private static async Task<Guid> AdministratorAssignmentAsync(HttpClient admin, Guid userId)
    {
        var members = await Json(await admin.GetAsync("/api/v1/members"));
        return members.EnumerateArray().Single(m => m.GetProperty("userId").GetGuid() == userId)
            .GetProperty("roleAssignments").EnumerateArray()
            .Single(r => r.GetProperty("role").GetString() == "administrator" && r.GetProperty("scopeType").GetString() == "organization")
            .GetProperty("id").GetGuid();
    }

    // --- Operador de plataforma ---

    [Fact]
    public async Task Host_command_creates_the_operator_with_the_password_from_standard_input()
    {
        using var factory = new NexgateFactory();
        var email = NexgateFactory.NewEmail("cli");
        var output = new StringWriter();

        var exit = await AdminCommands.CreatePlatformOperatorAsync(factory.Services,
            [AdminCommands.CreatePlatformOperator, "--email", email], new StringReader(NexgateFactory.Password + "\n"), output);

        Assert.Equal(0, exit);
        Assert.DoesNotContain(email, output.ToString());
        Assert.DoesNotContain(NexgateFactory.Password, output.ToString());
        using var client = factory.CreateClientWithToken((await factory.SignInAsync(email)).AccessToken);
        var created = await client.PostAsJsonAsync("/api/v1/platform/organizations",
            new { name = "Creada por CLI", administratorEmail = NexgateFactory.NewEmail() });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Host_command_promotes_an_existing_account_without_changing_its_password()
    {
        using var factory = new NexgateFactory();
        var admin = factory.Administrator(await factory.CreateOrganizationAsync());

        var exit = await AdminCommands.CreatePlatformOperatorAsync(factory.Services,
            [AdminCommands.CreatePlatformOperator, "--email", admin.Email], new StringReader("otra-contraseña-ignorada\n"), new StringWriter());

        Assert.Equal(0, exit);
        Assert.Equal(HttpStatusCode.OK, (await factory.PostSignInAsync(admin.Email, admin.Password)).StatusCode);
    }

    [Fact]
    public async Task Host_command_without_email_shows_usage()
    {
        using var factory = new NexgateFactory();
        var output = new StringWriter();

        var exit = await AdminCommands.CreatePlatformOperatorAsync(factory.Services, [AdminCommands.CreatePlatformOperator],
            new StringReader(string.Empty), output);

        Assert.Equal(2, exit);
        Assert.Contains("--email", output.ToString());
    }

    [Fact]
    public async Task Operator_without_memberships_has_no_access_to_business_data()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var client = await factory.CreateOperatorClientAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/sites")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/organization")).StatusCode);
        var signIn = await factory.PostSignInAsync((await factory.OperatorAsync()).Email, NexgateFactory.Password, organization);
        Assert.Equal(HttpStatusCode.Forbidden, signIn.StatusCode);
    }

    [Fact]
    public async Task Operator_reissues_an_administrator_invitation_without_gaining_access()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var client = await factory.CreateOperatorClientAsync();
        var email = NexgateFactory.NewEmail("admin2");

        var response = await client.PostAsJsonAsync($"/api/v1/platform/organizations/{organization}/administrator-invitations", new { email });
        var invitation = await Json(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("administrator", invitation.GetProperty("role").GetString());
        Assert.Equal("organization", invitation.GetProperty("scopeType").GetString());
        var newAdmin = await factory.AcceptAndSignInAsync(invitation.GetProperty("token").GetString()!, email, organization);
        using var newAdminClient = factory.CreateClientWithToken(newAdmin.Tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await newAdminClient.GetAsync("/api/v1/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/members")).StatusCode);
    }

    [Fact]
    public async Task Administrator_invitation_for_a_missing_organization_responds_404()
    {
        using var factory = new NexgateFactory();
        using var client = await factory.CreateOperatorClientAsync();

        var response = await client.PostAsJsonAsync($"/api/v1/platform/organizations/{Guid.NewGuid()}/administrator-invitations",
            new { email = NexgateFactory.NewEmail() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Non_operators_cannot_request_administrator_invitations()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);

        var response = await admin.PostAsJsonAsync($"/api/v1/platform/organizations/{organization}/administrator-invitations",
            new { email = NexgateFactory.NewEmail() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- Alcance de sitio en los endpoints de Tenancy ---

    [Fact]
    public async Task Site_scoped_reader_sees_only_its_site()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var mine = await SiteAsync(admin, "Torre propia");
        var other = await SiteAsync(admin, "Torre vecina");
        var guard = await factory.CreateUserAsync(organization, "guard", "site", mine);
        using var client = factory.CreateClientWithToken(guard.Tokens.AccessToken);

        var list = await Json(await client.GetAsync("/api/v1/sites"));
        var outside = await client.GetAsync($"/api/v1/sites/{other}");
        var missing = await client.GetAsync($"/api/v1/sites/{Guid.NewGuid()}");

        Assert.Equal([mine], list.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).ToArray());
        Assert.Equal(HttpStatusCode.NotFound, outside.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/sites/{mine}")).StatusCode);
        var permissions = await PermissionsAsync(factory, guard.Tokens.AccessToken);
        Assert.False(permissions["sites.read"].GetProperty("organization").GetBoolean());
        Assert.Equal([mine], permissions["sites.read"].GetProperty("siteIds").EnumerateArray().Select(s => s.GetGuid()).ToArray());
    }

    [Fact]
    public async Task Site_scoped_manager_modifies_its_site_but_cannot_create_sites()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var mine = await SiteAsync(admin, "Torre propia");
        var other = await SiteAsync(admin, "Torre vecina");
        var siteAdmin = await factory.CreateUserAsync(organization, "administrator", "site", mine);
        using var client = factory.CreateClientWithToken(siteAdmin.Tokens.AccessToken);

        var update = await client.PutAsJsonAsync($"/api/v1/sites/{mine}", new { name = "Torre renombrada", timeZone = Cordoba });
        var create = await client.PostAsJsonAsync("/api/v1/sites", new { name = "Nueva", type = "building", timeZone = Cordoba });
        var outside = await client.PutAsJsonAsync($"/api/v1/sites/{other}", new { name = "Ajena", timeZone = Cordoba });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Torre renombrada", (await Json(update)).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outside.StatusCode);
        var unchanged = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/sites/{other}");
        Assert.Equal("Torre vecina", unchanged.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Reader_cannot_modify_a_site_it_can_read()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var site = await SiteAsync(admin, "Torre");
        var guard = await factory.CreateUserAsync(organization, "guard");
        using var client = factory.CreateClientWithToken(guard.Tokens.AccessToken);

        var response = await client.PutAsJsonAsync($"/api/v1/sites/{site}", new { name = "Cambio", timeZone = Cordoba });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Torre", (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/sites/{site}")).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Reader_with_a_site_scoped_manager_role_gets_403_on_readable_sites_it_cannot_manage()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var managed = await SiteAsync(admin, "Gestionada");
        var readable = await SiteAsync(admin, "Solo lectura");
        var user = await factory.CreateUserAsync(organization, "guard");
        await admin.PostAsJsonAsync($"/api/v1/members/{user.UserId}/role-assignments",
            new { role = "administrator", scopeType = "site", siteId = managed });
        using var client = factory.CreateClientWithToken(user.Tokens.AccessToken);

        var response = await client.PutAsJsonAsync($"/api/v1/sites/{readable}", new { name = "Cambio", timeZone = Cordoba });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void Platform_operator_service_is_registered_for_the_host_command()
    {
        using var factory = new NexgateFactory();
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<Nexgate.Modules.Identity.Application.PlatformOperatorService>());
    }
}
