using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Nexgate.BuildingBlocks.Access;
using Nexgate.BuildingBlocks.Persistence;
using Nexgate.BuildingBlocks.Tenancy;
using Nexgate.Modules.Tenancy.Domain;
using Nexgate.Modules.Tenancy.Infrastructure;
using Npgsql;

namespace Nexgate.Host.Tests;

/// <summary>
/// Aislamiento garantizado por la base de datos: estas pruebas se conectan con el rol de la aplicación y usan
/// SQL directo, sin los filtros de EF, para simular un error de la aplicación.
/// </summary>
public class TenantIsolationTests
{
    private sealed record Tenant(Guid OrganizationId, Guid SiteId);

    private static async Task<(TestDatabase Db, Tenant A, Tenant B)> TwoTenantsAsync(NexgateFactory? existing = null)
    {
        var db = await TestDatabase.GetAsync();
        using var owned = existing is null ? new NexgateFactory(db) : null;
        var factory = existing ?? owned!;

        async Task<Tenant> Create(string name)
        {
            var organization = await factory.CreateOrganizationAsync(name);
            using var client = factory.CreateClientFor(organization);
            var site = await TenancyApiTests.CreateSiteAsync(client, $"Sitio {name}");
            return new Tenant(organization, site.GetProperty("id").GetGuid());
        }

        return (db, await Create("A"), await Create("B"));
    }

    private static async Task<NpgsqlConnection> OpenAsAppAsync(TestDatabase db, Guid? organization, int? maxPoolSize = null)
    {
        var connection = new NpgsqlConnection(db.AppConnection(maxPoolSize));
        await connection.OpenAsync();
        if (organization is { } id)
        {
            await using var set = new NpgsqlCommand("SELECT set_config('app.org_id', @id, false)", connection);
            set.Parameters.AddWithValue("id", id.ToString());
            await set.ExecuteNonQueryAsync();
        }

        return connection;
    }

    private static async Task<List<Guid>> SiteOrganizationsAsync(NpgsqlConnection connection)
    {
        await using var query = new NpgsqlCommand("SELECT organization_id FROM tenancy.site", connection);
        await using var reader = await query.ExecuteReaderAsync();
        var result = new List<Guid>();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetGuid(0));
        }

        return result;
    }

    [Fact]
    public async Task Query_without_an_organization_filter_returns_only_rows_of_the_active_organization()
    {
        var (db, a, _) = await TwoTenantsAsync();
        await using var connection = await OpenAsAppAsync(db, a.OrganizationId);

        var organizations = await SiteOrganizationsAsync(connection);

        Assert.NotEmpty(organizations);
        Assert.All(organizations, id => Assert.Equal(a.OrganizationId, id));

        await using var orgs = new NpgsqlCommand("SELECT id FROM tenancy.organization", connection);
        Assert.Equal(a.OrganizationId, (Guid)(await orgs.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Inserting_a_row_for_another_organization_is_rejected_by_the_database()
    {
        var (db, a, b) = await TwoTenantsAsync();
        await using var connection = await OpenAsAppAsync(db, a.OrganizationId);

        await using var insert = new NpgsqlCommand(
            "INSERT INTO tenancy.site (id, organization_id, name, type, time_zone, configuration, created_at, updated_at) " +
            "VALUES (gen_random_uuid(), @other, 'Intruso', 'house', 'UTC', '{}', now(), now())", connection);
        insert.Parameters.AddWithValue("other", b.OrganizationId);

        var error = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task Moving_a_row_to_another_organization_is_rejected_by_the_database()
    {
        var (db, a, b) = await TwoTenantsAsync();
        await using var connection = await OpenAsAppAsync(db, a.OrganizationId);

        await using var update = new NpgsqlCommand("UPDATE tenancy.site SET organization_id = @other WHERE id = @site", connection);
        update.Parameters.AddWithValue("other", b.OrganizationId);
        update.Parameters.AddWithValue("site", a.SiteId);

        var error = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task Rows_of_another_organization_cannot_be_updated_or_deleted()
    {
        var (db, a, b) = await TwoTenantsAsync();
        await using var connection = await OpenAsAppAsync(db, a.OrganizationId);

        await using var update = new NpgsqlCommand("UPDATE tenancy.site SET name = 'Pisado' WHERE id = @site", connection);
        update.Parameters.AddWithValue("site", b.SiteId);
        await using var delete = new NpgsqlCommand("DELETE FROM tenancy.site WHERE id = @site", connection);
        delete.Parameters.AddWithValue("site", b.SiteId);

        Assert.Equal(0, await update.ExecuteNonQueryAsync());
        Assert.Equal(0, await delete.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task Organizations_can_only_be_inserted_as_a_platform_operation()
    {
        var db = await TestDatabase.GetAsync();
        await using var connection = await OpenAsAppAsync(db, organization: null);

        await using var insert = new NpgsqlCommand(
            "INSERT INTO tenancy.organization (id, name, status, created_at) VALUES (gen_random_uuid(), 'Pirata', 'active', now())",
            connection);

        var error = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task Platform_operation_flag_does_not_expose_business_data()
    {
        var (db, _, _) = await TwoTenantsAsync();
        await using var connection = await OpenAsAppAsync(db, organization: null);
        await using var flag = new NpgsqlCommand("SELECT set_config('app.platform_operation', 'on', false)", connection);
        await flag.ExecuteNonQueryAsync();

        Assert.Empty(await SiteOrganizationsAsync(connection));
        await using var orgs = new NpgsqlCommand("SELECT count(*) FROM tenancy.organization", connection);
        Assert.Equal(0L, await orgs.ExecuteScalarAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Without_an_active_organization_queries_return_no_rows_and_do_not_fail(bool setToEmpty)
    {
        var (db, _, _) = await TwoTenantsAsync();
        await using var connection = await OpenAsAppAsync(db, organization: null);
        if (setToEmpty)
        {
            // Estado de una conexión que ya tuvo una organización: la variable queda como cadena vacía.
            await using var clear = new NpgsqlCommand("SELECT set_config('app.org_id', '', false)", connection);
            await clear.ExecuteNonQueryAsync();
        }

        Assert.Empty(await SiteOrganizationsAsync(connection));
    }

    [Fact]
    public async Task Active_organization_does_not_leak_to_the_next_use_of_a_pooled_connection()
    {
        var (db, a, _) = await TwoTenantsAsync();
        var poolOfOne = db.AppConnection(maxPoolSize: 1);

        // Solicitud de la organización A (con EF y el interceptor), y después una sin organización.
        await using (var withOrganization = CreateContext(poolOfOne, a.OrganizationId))
        {
            Assert.NotEmpty(await withOrganization.Sites.ToListAsync());
        }

        await using (var withoutOrganization = CreateContext(poolOfOne, organization: null))
        {
            Assert.Empty(await withoutOrganization.Sites.IgnoreQueryFilters().ToListAsync());
        }

        // Y con SQL directo sobre la misma conexión física, sin fijar nada.
        await using var raw = new NpgsqlConnection(poolOfOne);
        await raw.OpenAsync();
        Assert.Empty(await SiteOrganizationsAsync(raw));
    }

    [Theory]
    [InlineData("ALTER TABLE tenancy.site DISABLE ROW LEVEL SECURITY")]
    [InlineData("ALTER TABLE tenancy.site NO FORCE ROW LEVEL SECURITY")]
    [InlineData("DROP POLICY tenant_isolation ON tenancy.site")]
    [InlineData("ALTER TABLE tenancy.site ADD COLUMN backdoor text")]
    [InlineData("SET ROLE nexgate_owner")]
    [InlineData("CREATE TABLE tenancy.shadow (id int)")]
    [InlineData("SELECT * FROM tenancy.__ef_migrations_history")]
    public async Task Application_role_cannot_bypass_isolation(string sql)
    {
        var db = await TestDatabase.GetAsync();
        await using var connection = await OpenAsAppAsync(db, organization: null);
        await using var command = new NpgsqlCommand(sql, connection);

        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Contains(error.SqlState, new[] { PostgresErrorCodes.InsufficientPrivilege, "42501" });
    }

    [Fact]
    public async Task Resource_of_another_organization_and_a_nonexistent_one_get_the_same_response()
    {
        using var factory = new NexgateFactory();
        var (_, a, b) = await TwoTenantsAsync(factory);
        using var client = factory.CreateClientFor(a.OrganizationId);

        var foreign = await client.GetAsync($"/api/v1/sites/{b.SiteId}");
        var missing = await client.GetAsync($"/api/v1/sites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(missing.StatusCode, foreign.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(foreign));
    }

    private static async Task<string> WithoutPerRequestFields(HttpResponseMessage response)
    {
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        json.Remove("correlationId");
        json.Remove("traceId");
        return json.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static TenancyDbContext CreateContext(string connectionString, Guid? organization, Guid? user = null)
    {
        var current = new FixedOrganization(organization, user);
        var options = new DbContextOptionsBuilder<TenancyDbContext>();
        PersistenceExtensions.ConfigureNpgsql(options, connectionString, TenancyDbContext.Schema);
        options.AddInterceptors(new SessionContextInterceptor(current, current));
        return new TenancyDbContext(options.Options, current);
    }

    [Fact]
    public async Task Current_user_does_not_leak_to_the_next_use_of_a_pooled_connection()
    {
        var db = await TestDatabase.GetAsync();
        var poolOfOne = db.AppConnection(maxPoolSize: 1);
        var user = Guid.NewGuid();

        await using (var withUser = CreateContext(poolOfOne, organization: null, user))
        {
            Assert.Equal(user.ToString(), await withUser.Database.SqlQuery<string>($"SELECT public.nexgate_current_user()::text AS \"Value\"").SingleAsync());
        }

        await using (var withoutUser = CreateContext(poolOfOne, organization: null))
        {
            Assert.Null(await withoutUser.Database.SqlQuery<string?>($"SELECT public.nexgate_current_user()::text AS \"Value\"").SingleAsync());
        }

        await using var raw = new NpgsqlConnection(poolOfOne);
        await raw.OpenAsync();
        await using var query = new NpgsqlCommand("SELECT public.nexgate_current_user()", raw);
        Assert.Equal(DBNull.Value, await query.ExecuteScalarAsync());
    }

    internal sealed class FixedOrganization(Guid? id, Guid? user = null) : ICurrentOrganization, ICurrentUser
    {
        public Guid? Id => id;

        Guid? ICurrentUser.Id => user;
    }
}

/// <summary>Segunda barrera: el DbContext base rechaza escrituras fuera de la organización activa antes de ir a la base.</summary>
public class TenantDbContextRulesTests
{
    private static TenancyDbContext Context(Guid? organization)
    {
        var current = new TenantIsolationTests.FixedOrganization(organization);
        var options = new DbContextOptionsBuilder<TenancyDbContext>();
        PersistenceExtensions.ConfigureNpgsql(options, NexgateFactory.UnreachableDatabase, TenancyDbContext.Schema);
        return new TenancyDbContext(options.Options, current);
    }

    private static Site NewSite(Guid organization) =>
        Site.Create(organization, "Sitio", SiteType.Building, "UTC", null, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Creating_business_data_without_an_active_organization_fails()
    {
        await using var db = Context(organization: null);
        db.Sites.Add(NewSite(Guid.NewGuid()));

        await Assert.ThrowsAsync<TenantIsolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Creating_business_data_for_another_organization_fails()
    {
        await using var db = Context(Guid.NewGuid());
        db.Sites.Add(NewSite(Guid.NewGuid()));

        await Assert.ThrowsAsync<TenantIsolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Changing_the_organization_of_existing_data_fails()
    {
        var organization = Guid.NewGuid();
        await using var db = Context(organization);
        var site = NewSite(organization);
        db.Sites.Attach(site);
        db.Entry(site).Property(s => s.OrganizationId).CurrentValue = Guid.NewGuid();

        await Assert.ThrowsAsync<TenantIsolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void Business_entities_get_a_global_organization_filter()
    {
        using var db = Context(Guid.NewGuid());

        Assert.NotEmpty(db.Model.FindEntityType(typeof(Site))!.GetDeclaredQueryFilters());
        Assert.NotEmpty(db.Model.FindEntityType(typeof(Organization))!.GetDeclaredQueryFilters());
    }
}
