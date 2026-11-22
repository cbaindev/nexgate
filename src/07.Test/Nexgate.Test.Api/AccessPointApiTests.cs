using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Nexgate.Test.Api.SpacesApiTests;

namespace Nexgate.Test.Api;

/// <summary>Escenarios de la capacidad <c>access-points</c>, incluido el camino a un destino.</summary>
public class AccessPointApiTests
{
    internal static Task<HttpResponseMessage> PostPointAsync(HttpClient client, Guid siteId, object body) =>
        client.PostAsJsonAsync($"/api/v1/sites/{siteId}/access-points", body);

    internal static async Task<Guid> PointAsync(HttpClient client, Guid siteId, string name, Guid toSpaceId,
        string type = "door", Guid? fromSpaceId = null, string failMode = "fail_secure")
    {
        var response = await PostPointAsync(client, siteId, new { name, type, toSpaceId, fromSpaceId, failMode });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetGuid();
    }

    /// <summary>Sitio con raíz, torre A con piso 5 y unidad 5B, y torre B (el ejemplo del documento 02).</summary>
    private sealed record Building(Guid Site, Guid Root, Guid TowerA, Guid Floor5, Guid Unit5B, Guid TowerB);

    private static async Task<Building> BuildingAsync(HttpClient admin)
    {
        var site = await SiteAsync(admin);
        var root = await RootAsync(admin, site);
        var towerA = await SpaceAsync(admin, site, root, "building", "Torre A");
        var floor5 = await SpaceAsync(admin, site, towerA, "floor", "Piso 5");
        var unit = await SpaceAsync(admin, site, floor5, "unit", "5B");
        var towerB = await SpaceAsync(admin, site, root, "building", "Torre B");
        return new Building(site, root, towerA, floor5, unit, towerB);
    }

    // --- Alta ---

    [Fact]
    public async Task A_valid_access_point_is_created()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);

        var response = await PostPointAsync(admin, b.Site,
            new { name = "Puerta del hall", type = "door", toSpaceId = b.TowerA, fromSpaceId = b.Root, failMode = "fail_secure" });
        var body = await Json(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(b.Site, body.GetProperty("siteId").GetGuid());
        Assert.Equal("Puerta del hall", body.GetProperty("name").GetString());
        Assert.Equal("door", body.GetProperty("type").GetString());
        Assert.Equal(b.TowerA, body.GetProperty("toSpaceId").GetGuid());
        Assert.Equal(b.Root, body.GetProperty("fromSpaceId").GetGuid());
        Assert.Equal("fail_secure", body.GetProperty("failMode").GetString());
        Assert.Equal($"/api/v1/access-points/{body.GetProperty("id").GetGuid()}", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abierta")]
    public async Task The_fail_mode_must_be_stated(string? failMode)
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);

        var response = await PostPointAsync(admin, b.Site, new { name = "Salida", type = "door", toSpaceId = b.Root, failMode });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("FailMode", out _));
        Assert.Empty((await Json(await admin.GetAsync($"/api/v1/sites/{b.Site}/access-points"))).EnumerateArray());
    }

    [Theory]
    [InlineData("toSpaceId", "ToSpaceId")]
    [InlineData("fromSpaceId", "FromSpaceId")]
    public async Task Spaces_of_another_site_another_organization_or_missing_respond_the_same(string property, string field)
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreign = await RootAsync(other, await SiteAsync(other, "Ajeno"));
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        var otherSite = await RootAsync(admin, await SiteAsync(admin, "Otro"));

        object Body(Guid space) => property == "toSpaceId"
            ? new { name = "Puerta", type = "door", toSpaceId = space, failMode = "fail_safe" }
            : new { name = "Puerta", type = "door", toSpaceId = b.TowerA, fromSpaceId = space, failMode = "fail_safe" };

        var responses = new[]
        {
            await PostPointAsync(admin, b.Site, Body(otherSite)),
            await PostPointAsync(admin, b.Site, Body(foreign)),
            await PostPointAsync(admin, b.Site, Body(Guid.NewGuid()))
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        Assert.Single((await Task.WhenAll(responses.Select(WithoutPerRequestFields))).Distinct());
        Assert.True((await Json(responses[0])).GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task The_origin_cannot_be_the_target()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);

        var response = await PostPointAsync(admin, b.Site,
            new { name = "Puerta", type = "door", toSpaceId = b.TowerA, fromSpaceId = b.TowerA, failMode = "fail_safe" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("FromSpaceId", out _));
    }

    [Fact]
    public async Task Names_are_unique_in_the_site_ignoring_case()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        await PointAsync(admin, b.Site, "Portón principal", b.Root, "gate");
        var other = await PointAsync(admin, b.Site, "Puerta del hall", b.TowerA);

        var duplicate = await PostPointAsync(admin, b.Site, new { name = "PORTÓN principal", type = "gate", toSpaceId = b.Root, failMode = "fail_safe" });
        var rename = await admin.PutAsJsonAsync($"/api/v1/access-points/{other}",
            new { name = "portón principal", type = "door", toSpaceId = b.TowerA, failMode = "fail_safe" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("access_point_name_conflict", (await Json(duplicate)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, rename.StatusCode);
        Assert.Equal("Puerta del hall", (await Json(await admin.GetAsync($"/api/v1/access-points/{other}"))).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Site_of_another_organization_or_missing_responds_404()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreignSite = await SiteAsync(other, "Ajeno");
        var foreignRoot = await RootAsync(other, foreignSite);
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var foreign = await PostPointAsync(admin, foreignSite, new { name = "Portón", type = "gate", toSpaceId = foreignRoot, failMode = "fail_safe" });
        var missing = await PostPointAsync(admin, Guid.NewGuid(), new { name = "Portón", type = "gate", toSpaceId = foreignRoot, failMode = "fail_safe" });

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(foreign));
        Assert.Empty((await Json(await other.GetAsync($"/api/v1/sites/{foreignSite}/access-points"))).EnumerateArray());
    }

    // --- Listado, consulta, modificación y eliminación ---

    [Fact]
    public async Task Access_points_of_a_site_are_listed_by_name()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        var otherSite = await SiteAsync(admin, "Otro");
        await PointAsync(admin, otherSite, "Ajena", await RootAsync(admin, otherSite));
        await PointAsync(admin, b.Site, "Puerta del hall", b.TowerA);
        await PointAsync(admin, b.Site, "ascensor", b.Floor5, "elevator");
        await PointAsync(admin, b.Site, "Portón", b.Root, "gate");

        var list = (await Json(await admin.GetAsync($"/api/v1/sites/{b.Site}/access-points"))).EnumerateArray().ToList();

        Assert.Equal(["ascensor", "Portón", "Puerta del hall"], list.Select(p => p.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task An_access_point_is_modified_and_deleted()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        var point = await PointAsync(admin, b.Site, "Puerta", b.TowerA);

        var update = await admin.PutAsJsonAsync($"/api/v1/access-points/{point}",
            new { name = "Salida de emergencia", type = "door", toSpaceId = b.Root, fromSpaceId = b.TowerA, failMode = "fail_safe" });
        var body = await Json(update);
        var delete = await admin.DeleteAsync($"/api/v1/access-points/{point}");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(("Salida de emergencia", b.Root, b.TowerA, "fail_safe"),
            (body.GetProperty("name").GetString(), body.GetProperty("toSpaceId").GetGuid(), body.GetProperty("fromSpaceId").GetGuid(),
                body.GetProperty("failMode").GetString()));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/access-points/{point}")).StatusCode);
    }

    [Fact]
    public async Task An_access_point_of_another_organization_responds_like_a_missing_one()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreignSite = await SiteAsync(other);
        var foreignRoot = await RootAsync(other, foreignSite);
        var foreignPoint = await PointAsync(other, foreignSite, "Portón", foreignRoot, "gate");
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var get = await admin.GetAsync($"/api/v1/access-points/{foreignPoint}");
        var missing = await admin.GetAsync($"/api/v1/access-points/{Guid.NewGuid()}");
        var put = await admin.PutAsJsonAsync($"/api/v1/access-points/{foreignPoint}",
            new { name = "Pisado", type = "gate", toSpaceId = foreignRoot, failMode = "fail_safe" });
        var delete = await admin.DeleteAsync($"/api/v1/access-points/{foreignPoint}");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(get));
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal("Portón", (await Json(await other.GetAsync($"/api/v1/access-points/{foreignPoint}"))).GetProperty("name").GetString());
    }

    // --- Camino a un espacio ---

    private static async Task<List<string>> PathAsync(HttpClient client, Guid spaceId)
    {
        var response = await client.GetAsync($"/api/v1/spaces/{spaceId}/access-path");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Json(response)).EnumerateArray().Select(p => p.GetProperty("name").GetString()!).ToList();
    }

    [Fact]
    public async Task The_path_to_a_unit_goes_from_the_root_to_the_unit_door()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        // Se cargan desordenados a propósito.
        await PointAsync(admin, b.Site, "Puerta del 5B", b.Unit5B);
        await PointAsync(admin, b.Site, "Puerta del hall A", b.TowerA);
        await PointAsync(admin, b.Site, "Portón principal", b.Root, "gate");
        await PointAsync(admin, b.Site, "Puerta del piso 5", b.Floor5);
        await PointAsync(admin, b.Site, "Puerta del hall B", b.TowerB);

        Assert.Equal(["Portón principal", "Puerta del hall A", "Puerta del piso 5", "Puerta del 5B"], await PathAsync(admin, b.Unit5B));
        Assert.Equal(["Portón principal", "Puerta del hall A"], await PathAsync(admin, b.TowerA));

        var entries = (await Json(await admin.GetAsync($"/api/v1/spaces/{b.Unit5B}/access-path"))).EnumerateArray().ToList();
        Assert.Equal([0, 1, 2, 3], entries.Select(e => e.GetProperty("toSpaceDepth").GetInt32()));
    }

    [Fact]
    public async Task Access_points_at_the_same_depth_are_ordered_by_name()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        await PointAsync(admin, b.Site, "Portón vehicular", b.Root, "gate");
        await PointAsync(admin, b.Site, "Molinete peatonal", b.Root, "turnstile");

        Assert.Equal(["Molinete peatonal", "Portón vehicular"], await PathAsync(admin, b.TowerB));
    }

    [Fact]
    public async Task A_path_without_access_points_is_empty()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        await PointAsync(admin, b.Site, "Puerta del hall B", b.TowerB);

        Assert.Empty(await PathAsync(admin, b.Unit5B));
    }

    [Fact]
    public async Task The_path_to_a_space_out_of_reach_responds_404()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreign = await RootAsync(other, await SiteAsync(other));
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var siteA = await SiteAsync(admin, "A");
        var rootB = await RootAsync(admin, await SiteAsync(admin, "B"));
        var guard = await factory.CreateUserAsync(organization, "guard", "site", siteA);
        using var guardClient = factory.CreateClientWithToken(guard.Tokens.AccessToken);

        var missing = await admin.GetAsync($"/api/v1/spaces/{Guid.NewGuid()}/access-path");
        var responses = new[]
        {
            await admin.GetAsync($"/api/v1/spaces/{foreign}/access-path"),
            await guardClient.GetAsync($"/api/v1/spaces/{rootB}/access-path")
        };

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(response));
        }
    }

    // --- Permisos y alcance por sitio ---

    [Fact]
    public async Task A_site_scoped_guard_reads_only_the_access_points_of_its_site()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var siteA = await SiteAsync(admin, "A");
        var siteB = await SiteAsync(admin, "B");
        var rootA = await RootAsync(admin, siteA);
        var pointA = await PointAsync(admin, siteA, "Portón A", rootA, "gate");
        var pointB = await PointAsync(admin, siteB, "Portón B", await RootAsync(admin, siteB), "gate");
        var guard = await factory.CreateUserAsync(organization, "guard", "site", siteA);
        using var client = factory.CreateClientWithToken(guard.Tokens.AccessToken);

        Assert.Single((await Json(await client.GetAsync($"/api/v1/sites/{siteA}/access-points"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/access-points/{pointA}")).StatusCode);
        Assert.Equal(["Portón A"], await PathAsync(client, rootA));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/sites/{siteB}/access-points")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/access-points/{pointB}")).StatusCode);

        var create = await PostPointAsync(client, siteA, new { name = "Otra", type = "door", toSpaceId = rootA, failMode = "fail_safe" });
        var update = await client.PutAsJsonAsync($"/api/v1/access-points/{pointA}",
            new { name = "Pisado", type = "gate", toSpaceId = rootA, failMode = "fail_safe" });
        var delete = await client.DeleteAsync($"/api/v1/access-points/{pointA}");
        Assert.All([create, update, delete], r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        Assert.Equal("Portón A", (await Json(await admin.GetAsync($"/api/v1/access-points/{pointA}"))).GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_site_scoped_administrator_manages_only_its_site()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var siteA = await SiteAsync(admin, "A");
        var siteB = await SiteAsync(admin, "B");
        var rootB = await RootAsync(admin, siteB);
        var siteAdmin = await factory.CreateUserAsync(organization, "administrator", "site", siteA);
        using var client = factory.CreateClientWithToken(siteAdmin.Tokens.AccessToken);

        var rootA = await RootAsync(client, siteA);
        await PointAsync(client, siteA, "Portón A", rootA, "gate");
        var foreign = await PostPointAsync(client, siteB, new { name = "Portón B", type = "gate", toSpaceId = rootB, failMode = "fail_safe" });

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
