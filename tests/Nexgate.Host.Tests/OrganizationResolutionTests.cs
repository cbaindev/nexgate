using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Nexgate.BuildingBlocks.Persistence;
using Nexgate.BuildingBlocks.Tenancy;

namespace Nexgate.Host.Tests;

public class OrganizationResolutionTests
{
    [Fact]
    public async Task Organization_header_is_ignored_when_the_development_mechanism_is_disabled()
    {
        using var factory = new NexgateFactory { DevelopmentAccess = false };
        using var client = factory.CreateClientFor(Guid.NewGuid());

        var response = await client.GetAsync("/api/v1/sites");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void Application_refuses_to_start_with_the_development_mechanism_outside_development()
    {
        using var factory = new NexgateFactory { Environment = "Production", DevelopmentAccess = true };

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var validation = Assert.IsType<OptionsValidationException>(error.GetBaseException());
        Assert.Contains("Tenancy:DevelopmentAccess:Enabled", validation.Message);
        Assert.DoesNotContain("Password", validation.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Application_starts_outside_development_with_the_mechanism_disabled()
    {
        using var factory = new NexgateFactory { Environment = "Production", DevelopmentAccess = false };

        using var client = factory.CreateClient();
    }

    [Fact]
    public async Task Logs_of_a_request_carry_the_resolved_organization_identifier_but_not_its_name()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync("Nombre Reservado SA");
        using var client = factory.CreateClientFor(organization);

        await client.GetAsync(TestEndpoints.Ping);

        var log = Assert.Single(factory.Logs.Entries, e => e.Message == TestEndpoints.PingLogMessage);
        Assert.Equal(organization.ToString(), log.Scope["OrganizationId"]);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.AllText.Contains("Nombre Reservado"));
    }

    [Fact]
    public async Task Organization_claim_of_an_authenticated_user_is_the_active_organization()
    {
        var organization = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(OrganizationResolutionMiddleware.OrganizationClaim, organization.ToString())], "test"))
        };

        await Resolve(context, developmentAccess: false);

        Assert.Equal(organization, context.Items[OrganizationContextKeys.OrganizationId]);
    }

    [Fact]
    public async Task Authenticated_user_cannot_override_its_organization_with_the_development_header()
    {
        var organization = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(OrganizationResolutionMiddleware.OrganizationClaim, organization.ToString())], "test"))
        };
        context.Request.Headers[OrganizationResolutionMiddleware.DevelopmentHeader] = Guid.NewGuid().ToString();

        await Resolve(context, developmentAccess: true);

        Assert.Equal(organization, context.Items[OrganizationContextKeys.OrganizationId]);
    }

    [Theory]
    [InlineData("no-es-un-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Malformed_development_header_leaves_no_active_organization(string header)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[OrganizationResolutionMiddleware.DevelopmentHeader] = header;

        await Resolve(context, developmentAccess: true);

        Assert.False(context.Items.ContainsKey(OrganizationContextKeys.OrganizationId));
    }

    private static Task Resolve(HttpContext context, bool developmentAccess)
    {
        var options = Options.Create(new TenancyOptions { DevelopmentAccess = { Enabled = developmentAccess } });
        return new OrganizationResolutionMiddleware(_ => Task.CompletedTask, options).InvokeAsync(context);
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
