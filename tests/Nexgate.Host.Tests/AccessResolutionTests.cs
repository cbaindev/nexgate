using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nexgate.BuildingBlocks.Access;
using Nexgate.BuildingBlocks.Persistence;

namespace Nexgate.Host.Tests;

/// <summary>La organización activa sale solo del token y exige membresía (tenant-isolation, D-04).</summary>
public class AccessResolutionTests
{
    [Fact]
    public async Task Organization_supplied_in_a_header_or_query_string_is_ignored()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var client = await factory.CreateOperatorClientAsync();
        client.DefaultRequestHeaders.Add("X-Nexgate-Organization", organization.ToString());

        var response = await client.GetAsync($"/api/v1/sites?organizationId={organization}&org_id={organization}");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("organization_context_required", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Anonymous_request_with_an_organization_header_has_no_active_organization()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Nexgate-Organization", organization.ToString());

        var response = await client.GetAsync("/api/v1/sites");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Application_refuses_to_start_outside_development_without_a_signing_key()
    {
        using var factory = new NexgateFactory { Environment = "Production" };

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var validation = Assert.IsType<OptionsValidationException>(error.GetBaseException());
        Assert.Contains("Identity:Tokens:SigningKey", validation.Message);
    }

    [Fact]
    public void Application_refuses_an_invalid_signing_key_without_showing_it()
    {
        const string bogus = "-----BEGIN EC PRIVATE KEY-----\nno-es-una-clave-secreta-123\n-----END EC PRIVATE KEY-----";
        using var factory = new NexgateFactory { Environment = "Production", SigningKey = bogus };

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var validation = Assert.IsType<OptionsValidationException>(error.GetBaseException());
        Assert.Contains("Identity:Tokens:SigningKey", validation.Message);
        Assert.DoesNotContain("no-es-una-clave-secreta-123", validation.Message);
    }

    [Fact]
    public async Task Application_starts_outside_development_with_a_signing_key_and_accepts_its_tokens()
    {
        using var factory = new NexgateFactory { Environment = "Production", SigningKey = NexgateFactory.NewSigningKey() };
        var organization = await factory.CreateOrganizationAsync();
        using var client = factory.CreateClientFor(organization);

        var response = await client.GetAsync("/api/v1/organization");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Logs_carry_the_organization_and_user_identifiers_but_not_the_organization_name_or_the_email()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync("Nombre Reservado SA");
        var admin = factory.Administrator(organization);
        using var client = factory.CreateClientFor(organization);

        await client.GetAsync(TestEndpoints.Ping);

        var log = Assert.Single(factory.Logs.Entries, e => e.Message == TestEndpoints.PingLogMessage);
        Assert.Equal(organization.ToString(), log.Scope["OrganizationId"]);
        Assert.Equal(admin.UserId.ToString(), log.Scope["UserId"]);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.AllText.Contains("Nombre Reservado"));
        Assert.DoesNotContain(factory.Logs.Entries, e => e.AllText.Contains(admin.Email, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Token_claims_set_the_user_and_the_organization_when_there_is_a_membership()
    {
        var user = Guid.NewGuid();
        var organization = Guid.NewGuid();
        var grants = new[] { new PermissionGrant("sites.read", null) };

        var context = await ResolveAsync(Principal(user, organization), (_, _) => grants);

        Assert.Equal(user, context.UserId);
        Assert.Equal(organization, context.OrganizationId);
        Assert.Equal(grants, context.Grants);
    }

    [Fact]
    public async Task Organization_claim_without_membership_leaves_no_active_organization()
    {
        var user = Guid.NewGuid();

        var context = await ResolveAsync(Principal(user, Guid.NewGuid()), (_, _) => null);

        Assert.Equal(user, context.UserId);
        Assert.Null(context.OrganizationId);
        Assert.Empty(context.Grants);
    }

    [Fact]
    public async Task Membership_is_checked_with_the_organization_already_in_the_context()
    {
        var organization = Guid.NewGuid();
        Guid? seen = null;

        await ResolveAsync(Principal(Guid.NewGuid(), organization), (_, ctx) =>
        {
            seen = ctx.OrganizationId;
            return [];
        });

        Assert.Equal(organization, seen);
    }

    [Theory]
    [InlineData("no-es-un-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Malformed_subject_leaves_an_anonymous_context(string subject)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(AccessClaims.Subject, subject), new Claim(AccessClaims.Organization, Guid.NewGuid().ToString())], "test"));

        var context = await ResolveAsync(principal, (_, _) => []);

        Assert.Null(context.UserId);
        Assert.Null(context.OrganizationId);
    }

    [Fact]
    public async Task Unauthenticated_principal_is_ignored_even_with_claims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(AccessClaims.Subject, Guid.NewGuid().ToString()), new Claim(AccessClaims.Organization, Guid.NewGuid().ToString())]));

        var context = await ResolveAsync(principal, (_, _) => []);

        Assert.Null(context.UserId);
        Assert.Null(context.OrganizationId);
    }

    private static ClaimsPrincipal Principal(Guid user, Guid organization) =>
        new(new ClaimsIdentity(
            [new Claim(AccessClaims.Subject, user.ToString()), new Claim(AccessClaims.Organization, organization.ToString())], "test"));

    private static async Task<RequestContext> ResolveAsync(ClaimsPrincipal principal,
        Func<Guid, RequestContext, IReadOnlyList<PermissionGrant>?> resolve)
    {
        var context = new RequestContext();
        var services = new ServiceCollection()
            .AddSingleton<IRequestAccessResolver>(new FakeResolver(context, resolve))
            .BuildServiceProvider();
        var http = new DefaultHttpContext { User = principal, RequestServices = services };

        await new RequestContextMiddleware(_ => Task.CompletedTask).InvokeAsync(http, context);
        return context;
    }

    private sealed class FakeResolver(RequestContext context, Func<Guid, RequestContext, IReadOnlyList<PermissionGrant>?> resolve)
        : IRequestAccessResolver
    {
        public Task<IReadOnlyList<PermissionGrant>?> ResolveAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(resolve(userId, context));
    }
}

public class TenantIsolationMigrationTests
{
    [Fact]
    public void Standard_policy_enables_forces_and_filters_by_organization()
    {
        var sql = TenantIsolationMigrationExtensions.TenantIsolationSql("spaces", "space");

        Assert.Equal(
        [
            "ALTER TABLE \"spaces\".\"space\" ENABLE ROW LEVEL SECURITY;",
            "ALTER TABLE \"spaces\".\"space\" FORCE ROW LEVEL SECURITY;",
            "CREATE POLICY tenant_isolation ON \"spaces\".\"space\" USING (organization_id = public.nexgate_current_org()) " +
            "WITH CHECK (organization_id = public.nexgate_current_org());"
        ], sql);
    }

    [Fact]
    public void Application_role_gets_data_access_but_no_access_to_the_migration_history()
    {
        var sql = TenantIsolationMigrationExtensions.GrantAppAccessSql("spaces");

        Assert.Contains("GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA \"spaces\" TO nexgate_app;", sql);
        Assert.Contains("ALTER DEFAULT PRIVILEGES IN SCHEMA \"spaces\" GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO nexgate_app;", sql);
        Assert.Equal("REVOKE ALL ON \"spaces\".\"__ef_migrations_history\" FROM nexgate_app;", sql[^1]);
        Assert.DoesNotContain(sql, s => s.Contains("TRUNCATE") || s.Contains("ALL PRIVILEGES"));
    }

    [Theory]
    [InlineData("Spaces")]
    [InlineData("spaces; drop table x")]
    [InlineData("")]
    public void Unsafe_identifiers_are_rejected(string schema)
    {
        Assert.Throws<ArgumentException>(() => TenantIsolationMigrationExtensions.TenantIsolationSql(schema, "space"));
    }
}
