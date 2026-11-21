using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nexgate.Host.Tests;

/// <summary>Escenarios de la capacidad <c>organization-membership</c>.</summary>
public class MembershipTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    // --- Invitaciones ---

    [Fact]
    public async Task Invitation_response_is_the_same_whether_or_not_the_email_has_an_account()
    {
        using var factory = new NexgateFactory();
        var other = await factory.CreateOrganizationAsync();
        var existing = factory.Administrator(other);
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);

        var forNew = await admin.PostAsJsonAsync("/api/v1/invitations",
            new { email = NexgateFactory.NewEmail("nuevo"), role = "guard", scopeType = "organization" });
        var forExisting = await admin.PostAsJsonAsync("/api/v1/invitations",
            new { email = existing.Email, role = "guard", scopeType = "organization" });

        Assert.Equal(HttpStatusCode.Created, forNew.StatusCode);
        Assert.Equal(forNew.StatusCode, forExisting.StatusCode);
        Assert.Equal(Shape(await forNew.Content.ReadAsStringAsync()), Shape(await forExisting.Content.ReadAsStringAsync()));
    }

    /// <summary>Forma del cuerpo sin los valores propios de cada invitación (id, correo, token, vencimiento).</summary>
    private static string Shape(string body)
    {
        var json = JsonNode.Parse(body)!.AsObject();
        foreach (var field in new[] { "id", "email", "token", "expiresAt" })
        {
            Assert.True(json.ContainsKey(field), $"Falta el campo {field}");
            json[field] = "*";
        }

        return json.ToJsonString();
    }

    [Theory]
    [InlineData("sin-arroba", "guard", "organization", "Email")]
    [InlineData("ok@example.com", "rey", "organization", "Role")]
    [InlineData("ok@example.com", "guard", "planeta", "ScopeType")]
    [InlineData("ok@example.com", "guard", "site", "SiteId")]
    public async Task Invalid_invitation_lists_the_invalid_field(string email, string role, string scopeType, string field)
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var response = await admin.PostAsJsonAsync("/api/v1/invitations", new { email, role, scopeType });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Invitation_to_a_site_of_another_organization_is_rejected_like_a_missing_site()
    {
        using var factory = new NexgateFactory();
        var other = await factory.CreateOrganizationAsync();
        using var otherAdmin = factory.CreateClientFor(other);
        var foreignSite = (await TenancyApiTests.CreateSiteAsync(otherAdmin, "Ajeno")).GetProperty("id").GetGuid();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var foreign = await admin.PostAsJsonAsync("/api/v1/invitations",
            new { email = NexgateFactory.NewEmail(), role = "guard", scopeType = "site", siteId = foreignSite });
        var missing = await admin.PostAsJsonAsync("/api/v1/invitations",
            new { email = NexgateFactory.NewEmail(), role = "guard", scopeType = "site", siteId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal((await Json(missing)).GetProperty("errors").ToString(), (await Json(foreign)).GetProperty("errors").ToString());
    }

    [Fact]
    public async Task Callers_without_member_management_at_organization_scope_cannot_invite()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var site = (await TenancyApiTests.CreateSiteAsync(admin, "Torre")).GetProperty("id").GetGuid();
        var guard = await factory.CreateUserAsync(organization, "guard");
        var siteAdmin = await factory.CreateUserAsync(organization, "administrator", "site", site);

        foreach (var user in new[] { guard, siteAdmin })
        {
            using var client = factory.CreateClientWithToken(user.Tokens.AccessToken);
            var response = await client.PostAsJsonAsync("/api/v1/invitations",
                new { email = NexgateFactory.NewEmail(), role = "guard", scopeType = "organization" });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("permission_denied", (await Json(response)).GetProperty("code").GetString());
        }
    }

    // --- Aceptación ---

    [Fact]
    public async Task Accepting_with_a_new_account_creates_account_membership_and_role()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();

        var guard = await factory.CreateUserAsync(organization, "guard");
        using var client = factory.CreateClientWithToken(guard.Tokens.AccessToken);
        var permissions = await Json(await client.GetAsync("/api/v1/me/permissions"));

        Assert.Equal(organization, guard.Tokens.OrganizationId);
        Assert.Equal(["organization.read", "sites.read"],
            permissions.GetProperty("permissions").EnumerateArray().Select(p => p.GetProperty("name").GetString()!).ToArray());
    }

    [Fact]
    public async Task Accepting_with_a_weak_password_creates_nothing_and_keeps_the_invitation()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        var invitation = await factory.InviteAsync(organization, NexgateFactory.NewEmail(), "guard");
        using var client = factory.CreateClient();

        var weak = await client.PostAsJsonAsync("/api/v1/invitations/accept", new { token = invitation.Token, password = "corta" });
        var strong = await client.PostAsJsonAsync("/api/v1/invitations/accept", new { token = invitation.Token, password = NexgateFactory.Password });

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.True((await Json(weak)).GetProperty("errors").TryGetProperty("Password", out _));
        Assert.Equal(HttpStatusCode.OK, strong.StatusCode);
    }

    [Fact]
    public async Task Existing_account_must_be_signed_in_to_accept_and_keeps_its_password()
    {
        using var factory = new NexgateFactory();
        var first = await factory.CreateOrganizationAsync();
        var second = await factory.CreateOrganizationAsync();
        var user = await factory.CreateUserAsync(first, "resident");
        var invitation = await factory.InviteAsync(second, user.Email.ToUpperInvariant(), "employee");

        using var anonymous = factory.CreateClient();
        var withoutSignIn = await anonymous.PostAsJsonAsync("/api/v1/invitations/accept",
            new { token = invitation.Token, password = "intento-de-pisar-la-clave" });
        var someoneElse = factory.CreateClientWithToken(factory.Administrator(second).Tokens.AccessToken);
        var asAnotherUser = await someoneElse.PostAsJsonAsync("/api/v1/invitations/accept", new { token = invitation.Token });
        using var signedIn = factory.CreateClientWithToken(user.Tokens.AccessToken);
        var accepted = await signedIn.PostAsJsonAsync("/api/v1/invitations/accept", new { token = invitation.Token });

        Assert.Equal(HttpStatusCode.Unauthorized, withoutSignIn.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, asAnotherUser.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(second, (await factory.SignInAsync(user.Email, user.Password, second)).OrganizationId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.PostSignInAsync(user.Email, "intento-de-pisar-la-clave")).StatusCode);
    }

    [Fact]
    public async Task Invalid_expired_and_used_tokens_get_the_same_response()
    {
        using var factory = new NexgateFactory();
        var db = await TestDatabase.GetAsync();
        var organization = await factory.CreateOrganizationAsync();
        var expired = await factory.InviteAsync(organization, NexgateFactory.NewEmail(), "guard");
        await db.ExecuteAsOwnerAsync("UPDATE identity.invitation SET expires_at = now() - interval '1 minute' WHERE id = @id",
            ["identity.invitation"], ("id", expired.Id));
        var used = await factory.InviteAsync(organization, NexgateFactory.NewEmail(), "guard");
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/v1/invitations/accept", new { token = used.Token, password = NexgateFactory.Password })).StatusCode);

        var bodies = new List<string>();
        foreach (var token in new[] { "no-existe", expired.Token, used.Token })
        {
            var response = await client.PostAsJsonAsync("/api/v1/invitations/accept", new { token, password = NexgateFactory.Password });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
            Assert.Equal("invalid_invitation", json["code"]!.GetValue<string>());
            json.Remove("correlationId");
            json.Remove("traceId");
            bodies.Add(json.ToJsonString());
        }

        Assert.Single(bodies.Distinct());
    }

    [Fact]
    public async Task Accepting_as_an_existing_member_keeps_one_membership_and_adds_the_role()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        var guard = await factory.CreateUserAsync(organization, "guard");
        var invitation = await factory.InviteAsync(organization, guard.Email, "employee");
        using var client = factory.CreateClientWithToken(guard.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/invitations/accept", new { token = invitation.Token })).StatusCode);

        using var admin = factory.CreateClientFor(organization);
        var members = (await Json(await admin.GetAsync("/api/v1/members"))).EnumerateArray()
            .Where(m => m.GetProperty("userId").GetGuid() == guard.UserId).ToList();
        var member = Assert.Single(members);
        Assert.Equal(["guard", "employee"],
            member.GetProperty("roleAssignments").EnumerateArray().Select(r => r.GetProperty("role").GetString()!).ToArray());
    }

    // --- Listado de miembros ---

    [Fact]
    public async Task Members_are_listed_with_their_roles_only_for_the_active_organization()
    {
        using var factory = new NexgateFactory();
        var (user, first, _) = await AuthenticationTests.UserInTwoOrganizationsAsync(factory);
        var unrelated = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(first);

        var members = (await Json(await admin.GetAsync("/api/v1/members"))).EnumerateArray().ToList();

        Assert.Equal(2, members.Count);
        var listed = Assert.Single(members, m => m.GetProperty("userId").GetGuid() == user.UserId);
        Assert.Equal(user.Email, listed.GetProperty("email").GetString());
        // Solo sus roles en esta organización (residente), no el de guardia de la otra.
        var role = Assert.Single(listed.GetProperty("roleAssignments").EnumerateArray());
        Assert.Equal("resident", role.GetProperty("role").GetString());
        Assert.DoesNotContain(members, m => m.GetProperty("userId").GetGuid() == factory.Administrator(unrelated).UserId);
    }

    [Fact]
    public async Task Members_cannot_be_listed_without_permission()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        var guard = await factory.CreateUserAsync(organization, "guard");
        using var client = factory.CreateClientWithToken(guard.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/members")).StatusCode);
    }

    // --- Baja de miembros ---

    [Fact]
    public async Task Removed_member_is_rejected_on_the_next_request_with_the_same_token()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        var guard = await factory.CreateUserAsync(organization, "guard");
        using var guardClient = factory.CreateClientWithToken(guard.Tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await guardClient.GetAsync("/api/v1/sites")).StatusCode);
        using var admin = factory.CreateClientFor(organization);

        var removed = await admin.DeleteAsync($"/api/v1/members/{guard.UserId}");
        var after = await guardClient.GetAsync("/api/v1/sites");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        Assert.Equal("organization_context_required", (await Json(after)).GetProperty("code").GetString());
        // La cuenta sigue existiendo: puede iniciar sesión, sin organización.
        Assert.Null((await factory.SignInAsync(guard.Email, guard.Password)).OrganizationId);
    }

    [Fact]
    public async Task Removing_a_user_of_another_organization_responds_like_a_missing_user()
    {
        using var factory = new NexgateFactory();
        var other = factory.Administrator(await factory.CreateOrganizationAsync());
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var foreign = await admin.DeleteAsync($"/api/v1/members/{other.UserId}");
        var missing = await admin.DeleteAsync($"/api/v1/members/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        // La cuenta ajena sigue intacta.
        Assert.Equal(HttpStatusCode.OK, (await factory.PostSignInAsync(other.Email, other.Password)).StatusCode);
    }

    [Fact]
    public async Task The_last_administrator_cannot_be_removed()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        var admin = factory.Administrator(organization);
        using var client = factory.CreateClientFor(organization);

        var response = await client.DeleteAsync($"/api/v1/members/{admin.UserId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("last_administrator", (await Json(response)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/members")).StatusCode);
    }
}
