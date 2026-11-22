using Npgsql;
using static Nexgate.Test.Api.SpacesApiTests;

namespace Nexgate.Test.Api;

/// <summary>
/// Aislamiento y reglas de mismo sitio de Spaces garantizados por la base: rol de la aplicación y SQL directo, sin los
/// filtros de EF, para simular un error de la aplicación.
/// </summary>
public class SpacesIsolationTests
{
    private sealed record Tenant(Guid OrganizationId, Guid SiteId, Guid RootId, Guid PointId);

    private static async Task<(TestDatabase Db, Tenant A, Tenant B, Guid OtherSiteRootOfA)> TwoTenantsAsync()
    {
        var db = await TestDatabase.GetAsync();
        using var factory = new NexgateFactory(db);

        async Task<(Tenant Tenant, HttpClient Client)> Create(string name)
        {
            var organization = await factory.CreateOrganizationAsync(name);
            var client = factory.CreateClientFor(organization);
            var site = await SiteAsync(client, $"Sitio {name}");
            var root = await RootAsync(client, site);
            var point = await AccessPointApiTests.PointAsync(client, site, "Portón", root, "gate");
            return (new Tenant(organization, site, root, point), client);
        }

        var (a, clientA) = await Create("A");
        var (b, clientB) = await Create("B");
        var otherRoot = await RootAsync(clientA, await SiteAsync(clientA, "Otro sitio A"));
        clientA.Dispose();
        clientB.Dispose();
        return (db, a, b, otherRoot);
    }

    private static async Task<List<Guid>> OrganizationsOfAsync(NpgsqlConnection connection, string table)
    {
        await using var query = new NpgsqlCommand($"SELECT organization_id FROM spaces.{table}", connection);
        await using var reader = await query.ExecuteReaderAsync();
        var result = new List<Guid>();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetGuid(0));
        }

        return result;
    }

    [Theory]
    [InlineData("space")]
    [InlineData("access_point")]
    public async Task Query_without_a_filter_returns_only_rows_of_the_active_organization(string table)
    {
        var (db, a, _, _) = await TwoTenantsAsync();
        await using var connection = await db.OpenAsAppAsync(a.OrganizationId);

        var organizations = await OrganizationsOfAsync(connection, table);

        Assert.NotEmpty(organizations);
        Assert.All(organizations, id => Assert.Equal(a.OrganizationId, id));
    }

    [Fact]
    public async Task Writing_spaces_with_another_organization_is_rejected()
    {
        var (db, a, b, _) = await TwoTenantsAsync();
        await using var connection = await db.OpenAsAppAsync(a.OrganizationId);

        await using var insert = new NpgsqlCommand(
            "INSERT INTO spaces.space (id, organization_id, site_id, parent_id, path, type, name, is_common, created_at, updated_at) " +
            "VALUES (@id, @other, @site, NULL, @path::ltree, 'site', 'Intruso', false, now(), now())", connection);
        var id = Guid.NewGuid();
        insert.Parameters.AddWithValue("id", id);
        insert.Parameters.AddWithValue("other", b.OrganizationId);
        insert.Parameters.AddWithValue("site", Guid.NewGuid());
        insert.Parameters.AddWithValue("path", id.ToString("N"));
        await using var move = new NpgsqlCommand("UPDATE spaces.space SET organization_id = @other WHERE id = @root", connection);
        move.Parameters.AddWithValue("other", b.OrganizationId);
        move.Parameters.AddWithValue("root", a.RootId);

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync())).SqlState);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => move.ExecuteNonQueryAsync())).SqlState);
    }

    [Fact]
    public async Task Writing_access_points_with_another_organization_is_rejected()
    {
        var (db, a, b, _) = await TwoTenantsAsync();
        await using var connection = await db.OpenAsAppAsync(a.OrganizationId);

        await using var move = new NpgsqlCommand("UPDATE spaces.access_point SET organization_id = @other WHERE id = @point", connection);
        move.Parameters.AddWithValue("other", b.OrganizationId);
        move.Parameters.AddWithValue("point", a.PointId);
        await using var foreignUpdate = new NpgsqlCommand("UPDATE spaces.access_point SET name = 'Pisado' WHERE id = @point", connection);
        foreignUpdate.Parameters.AddWithValue("point", b.PointId);

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => move.ExecuteNonQueryAsync())).SqlState);
        Assert.Equal(0, await foreignUpdate.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task An_access_point_cannot_target_a_space_of_another_site_even_with_direct_sql()
    {
        var (db, a, _, otherSiteRoot) = await TwoTenantsAsync();
        await using var connection = await db.OpenAsAppAsync(a.OrganizationId);

        await using var update = new NpgsqlCommand("UPDATE spaces.access_point SET to_space_id = @space WHERE id = @point", connection);
        update.Parameters.AddWithValue("space", otherSiteRoot);
        update.Parameters.AddWithValue("point", a.PointId);

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, (await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync())).SqlState);
    }

    [Fact]
    public async Task A_space_with_access_points_cannot_be_deleted_even_with_direct_sql()
    {
        var (db, a, _, _) = await TwoTenantsAsync();
        await using var connection = await db.OpenAsAppAsync(a.OrganizationId);

        await using var delete = new NpgsqlCommand("DELETE FROM spaces.space WHERE id = @root", connection);
        delete.Parameters.AddWithValue("root", a.RootId);

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, (await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync())).SqlState);
    }
}
