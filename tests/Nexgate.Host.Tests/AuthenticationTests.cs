using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Nexgate.Modules.Identity.Infrastructure;

namespace Nexgate.Host.Tests;

/// <summary>Escenarios de la capacidad <c>authentication</c>.</summary>
public class AuthenticationTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string> WithoutPerRequestFields(HttpResponseMessage response)
    {
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        json.Remove("correlationId");
        json.Remove("traceId");
        return json.ToJsonString();
    }

    // --- Inicio de sesión ---

    [Fact]
    public async Task Sign_in_with_a_single_membership_selects_that_organization()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        var admin = factory.Administrator(organization);

        var response = await factory.PostSignInAsync(admin.Email, admin.Password);
        var body = await Json(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        Assert.InRange(body.GetProperty("expiresIn").GetInt32(), 800, 900);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("refreshToken").GetString()));
        Assert.Equal(organization, body.GetProperty("organizationId").GetGuid());
        var token = new JsonWebToken(body.GetProperty("accessToken").GetString());
        Assert.Equal(organization.ToString(), token.GetClaim("org_id").Value);
        Assert.Equal(admin.UserId.ToString(), token.Subject);
        Assert.False(token.TryGetClaim("email", out _));
    }

    [Fact]
    public async Task Sign_in_with_several_memberships_without_choosing_has_no_active_organization()
    {
        using var factory = new NexgateFactory();
        var (user, first, second) = await UserInTwoOrganizationsAsync(factory);

        var tokens = await factory.SignInAsync(user.Email);
        using var client = factory.CreateClientWithToken(tokens.AccessToken);
        var sites = await client.GetAsync("/api/v1/sites");

        Assert.Null(tokens.OrganizationId);
        Assert.Equal(HttpStatusCode.Forbidden, sites.StatusCode);
        Assert.Equal("organization_context_required", (await Json(sites)).GetProperty("code").GetString());

        Assert.Equal(first, (await factory.SignInAsync(user.Email, organizationId: first)).OrganizationId);
        Assert.Equal(second, (await factory.SignInAsync(user.Email, organizationId: second)).OrganizationId);
    }

    [Fact]
    public async Task Choosing_an_organization_without_membership_or_nonexistent_is_rejected_the_same_way()
    {
        using var factory = new NexgateFactory();
        var mine = await factory.CreateOrganizationAsync();
        var other = await factory.CreateOrganizationAsync();
        var admin = factory.Administrator(mine);

        var foreign = await factory.PostSignInAsync(admin.Email, admin.Password, other);
        var missing = await factory.PostSignInAsync(admin.Email, admin.Password, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        var foreignBody = await WithoutPerRequestFields(foreign);
        Assert.Equal(await WithoutPerRequestFields(missing), foreignBody);
        var problem = JsonDocument.Parse(foreignBody).RootElement;
        Assert.Equal("organization_not_available", problem.GetProperty("code").GetString());
        Assert.False(problem.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task Wrong_password_unknown_email_and_disabled_account_get_the_same_response()
    {
        using var factory = new NexgateFactory();
        var db = await TestDatabase.GetAsync();
        var organization = await factory.CreateOrganizationAsync();
        var admin = factory.Administrator(organization);
        var disabled = await factory.CreateUserAsync(organization, "guard");
        await db.ExecuteAsOwnerAsync("UPDATE identity.user_account SET status = 'disabled' WHERE id = @id",
            ["identity.user_account"], ("id", disabled.UserId));

        var wrongPassword = await factory.PostSignInAsync(admin.Email, "otra-contraseña-larga");
        var unknown = await factory.PostSignInAsync(NexgateFactory.NewEmail("nadie"), NexgateFactory.Password);
        var disabledAccount = await factory.PostSignInAsync(disabled.Email, disabled.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal("invalid_credentials", (await Json(wrongPassword)).GetProperty("code").GetString());
        var expected = await WithoutPerRequestFields(wrongPassword);
        Assert.Equal(expected, await WithoutPerRequestFields(unknown));
        Assert.Equal(expected, await WithoutPerRequestFields(disabledAccount));
    }

    [Fact]
    public async Task Email_is_compared_without_regard_to_case()
    {
        using var factory = new NexgateFactory();
        var admin = factory.Administrator(await factory.CreateOrganizationAsync());

        var response = await factory.PostSignInAsync(admin.Email.ToUpperInvariant(), admin.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Repeated_sign_in_attempts_over_the_limit_get_429()
    {
        using var factory = new NexgateFactory { RateLimitPermits = 2 };
        var email = NexgateFactory.NewEmail();

        await factory.PostSignInAsync(email, "x");
        await factory.PostSignInAsync(email, "x");
        var limited = await factory.PostSignInAsync(email, "x");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("too_many_requests", (await Json(limited)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Sign_in_never_logs_the_email_or_the_password()
    {
        using var factory = new NexgateFactory();
        var admin = factory.Administrator(await factory.CreateOrganizationAsync());
        const string wrong = "contraseña-equivocada-987";

        await factory.PostSignInAsync(admin.Email, wrong);
        await factory.PostSignInAsync(admin.Email, admin.Password);

        Assert.NotEmpty(factory.Logs.Entries);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.AllText.Contains(admin.Email, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(factory.Logs.Entries, e => e.AllText.Contains(wrong) || e.AllText.Contains(admin.Password));
    }

    // --- Solicitudes autenticadas ---

    [Fact]
    public async Task Expired_tampered_or_foreign_tokens_get_401()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        var admin = factory.Administrator(organization);
        var key = factory.Services.GetRequiredService<TokenSigningKey>();

        var expired = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = TokenSigningKey.Issuer,
            Audience = TokenSigningKey.Audience,
            IssuedAt = DateTime.UtcNow.AddHours(-2),
            NotBefore = DateTime.UtcNow.AddHours(-2),
            Expires = DateTime.UtcNow.AddHours(-1),
            Claims = new Dictionary<string, object> { ["sub"] = admin.UserId.ToString(), ["org_id"] = organization.ToString() },
            SigningCredentials = new SigningCredentials(key.Key, SecurityAlgorithms.EcdsaSha256)
        });
        var valid = admin.Tokens.AccessToken;
        var tampered = valid[..^4] + (valid[^4] == 'A' ? "BBBB" : "AAAA");
        string foreign;
        using (var other = new NexgateFactory())
        {
            foreign = other.Administrator(await other.CreateOrganizationAsync()).Tokens.AccessToken;
        }

        foreach (var token in new[] { expired, tampered, foreign })
        {
            using var client = factory.CreateClientWithToken(token);
            var response = await client.GetAsync("/api/v1/organization");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("authentication_required", (await Json(response)).GetProperty("code").GetString());
        }
    }

    // --- Renovación y cambio de organización ---

    [Fact]
    public async Task Refresh_rotates_the_refresh_token()
    {
        using var factory = new NexgateFactory();
        var admin = factory.Administrator(await factory.CreateOrganizationAsync());
        using var client = factory.CreateClient();

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = admin.Tokens.RefreshToken });
        var body = await Json(refreshed);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.NotEqual(admin.Tokens.RefreshToken, body.GetProperty("refreshToken").GetString());
        Assert.Equal(admin.Tokens.OrganizationId, body.GetProperty("organizationId").GetGuid());
        using var withNew = factory.CreateClientWithToken(body.GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await withNew.GetAsync("/api/v1/organization")).StatusCode);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_whole_session()
    {
        using var factory = new NexgateFactory();
        var admin = factory.Administrator(await factory.CreateOrganizationAsync());
        using var client = factory.CreateClient();

        var first = await Json(await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = admin.Tokens.RefreshToken }));
        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = admin.Tokens.RefreshToken });
        var latest = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.GetProperty("refreshToken").GetString() });

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal("invalid_refresh_token", (await Json(reuse)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, latest.StatusCode);
    }

    [Fact]
    public async Task Refresh_can_switch_to_another_organization_with_membership()
    {
        using var factory = new NexgateFactory();
        var (user, first, second) = await UserInTwoOrganizationsAsync(factory);
        var tokens = await factory.SignInAsync(user.Email, organizationId: first);
        using var client = factory.CreateClient();

        var switched = await Json(await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken, organizationId = second }));
        using var inSecond = factory.CreateClientWithToken(switched.GetProperty("accessToken").GetString()!);
        var organization = await Json(await inSecond.GetAsync("/api/v1/organization"));

        Assert.Equal(second, switched.GetProperty("organizationId").GetGuid());
        Assert.Equal(second, organization.GetProperty("id").GetGuid());

        var notAllowed = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = switched.GetProperty("refreshToken").GetString(), organizationId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Forbidden, notAllowed.StatusCode);
        Assert.Equal("organization_not_available", (await Json(notAllowed)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Refresh_after_the_membership_ends_issues_no_token_for_that_organization()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        var guard = await factory.CreateUserAsync(organization, "guard");
        using var admin = factory.CreateClientFor(organization);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/members/{guard.UserId}")).StatusCode);
        using var client = factory.CreateClient();

        var kept = await Json(await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = guard.Tokens.RefreshToken }));
        var chosen = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = kept.GetProperty("refreshToken").GetString(), organizationId = organization });

        Assert.Equal(JsonValueKind.Null, kept.GetProperty("organizationId").ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, chosen.StatusCode);
    }

    // --- Cierre de sesión ---

    [Fact]
    public async Task Sign_out_revokes_the_session()
    {
        using var factory = new NexgateFactory();
        var admin = factory.Administrator(await factory.CreateOrganizationAsync());
        using var client = factory.CreateClient();

        var signOut = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = admin.Tokens.RefreshToken });
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = admin.Tokens.RefreshToken });
        var unknown = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = "no-existe" });

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);
    }

    // --- Usuario actual ---

    [Fact]
    public async Task Current_user_lists_only_its_own_organizations_with_their_names()
    {
        using var factory = new NexgateFactory();
        var (user, first, second) = await UserInTwoOrganizationsAsync(factory, "Alfa", "Beta");
        await factory.CreateOrganizationAsync("Gamma ajena");
        using var client = factory.CreateClientWithToken((await factory.SignInAsync(user.Email, organizationId: second)).AccessToken);

        var me = await Json(await client.GetAsync("/api/v1/me"));

        Assert.Equal(user.UserId, me.GetProperty("id").GetGuid());
        Assert.Equal(user.Email, me.GetProperty("email").GetString());
        Assert.Equal(second, me.GetProperty("activeOrganizationId").GetGuid());
        var organizations = me.GetProperty("organizations").EnumerateArray()
            .Select(o => (o.GetProperty("id").GetGuid(), o.GetProperty("name").GetString())).ToList();
        Assert.Equal([(first, "Alfa"), (second, "Beta")], organizations);
    }

    [Fact]
    public async Task Current_user_requires_authentication()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me/permissions")).StatusCode);
    }

    /// <summary>Un residente de la primera organización que además acepta una invitación de guardia en la segunda.</summary>
    internal static async Task<(TestUser User, Guid First, Guid Second)> UserInTwoOrganizationsAsync(NexgateFactory factory,
        string firstName = "Primera", string secondName = "Segunda")
    {
        var first = await factory.CreateOrganizationAsync(firstName);
        var second = await factory.CreateOrganizationAsync(secondName);
        var user = await factory.CreateUserAsync(first, "resident");
        var invitation = await factory.InviteAsync(second, user.Email, "guard");
        using var client = factory.CreateClientWithToken(user.Tokens.AccessToken);
        var accept = await client.PostAsJsonAsync("/api/v1/invitations/accept", new { token = invitation.Token });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        return (user, first, second);
    }
}
