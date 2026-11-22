using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nexgate.Test.Api;

/// <summary>Escenarios de la capacidad <c>space-tree</c>.</summary>
public class SpacesApiTests
{
    internal static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    internal static async Task<string> WithoutPerRequestFields(HttpResponseMessage response)
    {
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        json.Remove("correlationId");
        json.Remove("traceId");
        return json.ToJsonString();
    }

    internal static async Task<Guid> SiteAsync(HttpClient admin, string name = "Edificio") =>
        (await TenancyApiTests.CreateSiteAsync(admin, name)).GetProperty("id").GetGuid();

    internal static Task<HttpResponseMessage> PostSpaceAsync(HttpClient client, Guid siteId, object body) =>
        client.PostAsJsonAsync($"/api/v1/sites/{siteId}/spaces", body);

    internal static async Task<Guid> SpaceAsync(HttpClient client, Guid siteId, Guid? parentId, string type, string name, bool isCommon = false)
    {
        var response = await PostSpaceAsync(client, siteId, new { name, type, parentId, isCommon });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetGuid();
    }

    internal static Task<Guid> RootAsync(HttpClient client, Guid siteId, string name = "Edificio") =>
        SpaceAsync(client, siteId, null, "site", name);

    // --- Alta ---

    [Fact]
    public async Task Root_space_is_created()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);

        var response = await PostSpaceAsync(admin, site, new { name = "Edificio Centro", type = "site" });
        var body = await Json(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(site, body.GetProperty("siteId").GetGuid());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("parentId").ValueKind);
        Assert.Equal("site", body.GetProperty("type").GetString());
        Assert.Equal("Edificio Centro", body.GetProperty("name").GetString());
        Assert.False(body.GetProperty("isCommon").GetBoolean());
        Assert.Equal(0, body.GetProperty("depth").GetInt32());
        Assert.Equal($"/api/v1/spaces/{body.GetProperty("id").GetGuid()}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Child_space_has_its_parent_and_depth()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var root = await RootAsync(admin, site);

        var response = await PostSpaceAsync(admin, site, new { name = "Torre A", type = "building", parentId = root });
        var body = await Json(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(root, body.GetProperty("parentId").GetGuid());
        Assert.Equal(1, body.GetProperty("depth").GetInt32());
    }

    [Fact]
    public async Task A_site_has_a_single_root()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        await RootAsync(admin, site);

        var response = await PostSpaceAsync(admin, site, new { name = "Otra raíz", type = "site" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("space_root_exists", (await Json(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Two_roots_created_at_the_same_time_leave_only_one()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(i => PostSpaceAsync(admin, site, new { name = $"Raíz {i}", type = "site" })));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Single((await Json(await admin.GetAsync($"/api/v1/sites/{site}/spaces"))).EnumerateArray());
    }

    [Fact]
    public async Task The_site_type_is_only_for_the_root()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);

        var rootNotSite = await PostSpaceAsync(admin, site, new { name = "Torre", type = "building" });
        var root = await RootAsync(admin, site);
        var childSite = await PostSpaceAsync(admin, site, new { name = "Sub", type = "site", parentId = root });

        Assert.Equal(HttpStatusCode.BadRequest, rootNotSite.StatusCode);
        Assert.True((await Json(rootNotSite)).GetProperty("errors").TryGetProperty("Type", out _));
        Assert.Equal(HttpStatusCode.BadRequest, childSite.StatusCode);
        Assert.True((await Json(childSite)).GetProperty("errors").TryGetProperty("Type", out _));
    }

    [Fact]
    public async Task Parent_of_another_site_another_organization_or_missing_responds_the_same()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreignRoot = await RootAsync(other, await SiteAsync(other, "Ajeno"));
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin, "Norte");
        await RootAsync(admin, site);
        var otherSiteRoot = await RootAsync(admin, await SiteAsync(admin, "Sur"));

        var responses = new[]
        {
            await PostSpaceAsync(admin, site, new { name = "Torre", type = "building", parentId = otherSiteRoot }),
            await PostSpaceAsync(admin, site, new { name = "Torre", type = "building", parentId = foreignRoot }),
            await PostSpaceAsync(admin, site, new { name = "Torre", type = "building", parentId = Guid.NewGuid() })
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        var bodies = await Task.WhenAll(responses.Select(WithoutPerRequestFields));
        Assert.Single(bodies.Distinct());
        Assert.True((await Json(responses[0])).GetProperty("errors").TryGetProperty("ParentId", out _));
    }

    [Fact]
    public async Task Sibling_names_are_unique_ignoring_case_on_create_and_rename()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var root = await RootAsync(admin, site);
        await SpaceAsync(admin, site, root, "building", "Torre A");
        var towerB = await SpaceAsync(admin, site, root, "building", "Torre B");

        var duplicate = await PostSpaceAsync(admin, site, new { name = "TORRE a", type = "building", parentId = root });
        var rename = await admin.PutAsJsonAsync($"/api/v1/spaces/{towerB}", new { name = "torre A", type = "building" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("space_name_conflict", (await Json(duplicate)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, rename.StatusCode);
        Assert.Equal("space_name_conflict", (await Json(rename)).GetProperty("code").GetString());
        Assert.Equal("Torre B", (await Json(await admin.GetAsync($"/api/v1/spaces/{towerB}"))).GetProperty("name").GetString());
    }

    [Fact]
    public async Task The_same_name_is_allowed_under_different_parents()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var root = await RootAsync(admin, site);
        var towerA = await SpaceAsync(admin, site, root, "building", "Torre A");
        var towerB = await SpaceAsync(admin, site, root, "building", "Torre B");

        await SpaceAsync(admin, site, towerA, "floor", "Piso 1");
        await SpaceAsync(admin, site, towerB, "floor", "Piso 1");
    }

    [Fact]
    public async Task A_tree_deeper_than_the_limit_is_rejected()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var current = await RootAsync(admin, site);
        for (var depth = 1; depth <= 10; depth++)
        {
            current = await SpaceAsync(admin, site, current, "zone", $"Nivel {depth}");
        }

        var response = await PostSpaceAsync(admin, site, new { name = "Nivel 11", type = "zone", parentId = current });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("ParentId", out _));
    }

    [Fact]
    public async Task Invalid_name_and_type_are_listed()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);

        var response = await PostSpaceAsync(admin, site, new { name = new string('x', 201), type = "castillo" });
        var errors = (await Json(response)).GetProperty("errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("Type", out _));
    }

    [Fact]
    public async Task Site_of_another_organization_or_missing_responds_404()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreignSite = await SiteAsync(other, "Ajeno");
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var foreign = await PostSpaceAsync(admin, foreignSite, new { name = "Raíz", type = "site" });
        var missing = await PostSpaceAsync(admin, Guid.NewGuid(), new { name = "Raíz", type = "site" });
        var foreignList = await admin.GetAsync($"/api/v1/sites/{foreignSite}/spaces");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(foreign));
        Assert.Equal(HttpStatusCode.NotFound, foreignList.StatusCode);
        Assert.Empty((await Json(await other.GetAsync($"/api/v1/sites/{foreignSite}/spaces"))).EnumerateArray());
    }

    // --- Listado y consulta ---

    [Fact]
    public async Task Spaces_are_listed_in_tree_order()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var root = await RootAsync(admin, site);
        var towerB = await SpaceAsync(admin, site, root, "building", "Torre B");
        var towerA = await SpaceAsync(admin, site, root, "building", "torre a");
        var floor2 = await SpaceAsync(admin, site, towerA, "floor", "Piso 2");
        var floor1 = await SpaceAsync(admin, site, towerA, "floor", "Piso 1");

        var list = (await Json(await admin.GetAsync($"/api/v1/sites/{site}/spaces"))).EnumerateArray().ToList();

        Assert.Equal([root, towerA, floor1, floor2, towerB], list.Select(s => s.GetProperty("id").GetGuid()));
        Assert.Equal([0, 1, 2, 2, 1], list.Select(s => s.GetProperty("depth").GetInt32()));
    }

    [Fact]
    public async Task A_site_without_spaces_lists_none()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);

        var response = await admin.GetAsync($"/api/v1/sites/{site}/spaces");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await Json(response)).EnumerateArray());
    }

    [Fact]
    public async Task A_space_of_another_organization_responds_like_a_missing_one()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreignRoot = await RootAsync(other, await SiteAsync(other));
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var own = await RootAsync(admin, await SiteAsync(admin));

        var ownResponse = await admin.GetAsync($"/api/v1/spaces/{own}");
        var foreign = await admin.GetAsync($"/api/v1/spaces/{foreignRoot}");
        var missing = await admin.GetAsync($"/api/v1/spaces/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(foreign));
        foreach (var write in new[]
                 {
                     await admin.PutAsJsonAsync($"/api/v1/spaces/{foreignRoot}", new { name = "Pisado", type = "site" }),
                     await admin.DeleteAsync($"/api/v1/spaces/{foreignRoot}")
                 })
        {
            Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
        }

        Assert.Equal("Edificio", (await Json(await other.GetAsync($"/api/v1/spaces/{foreignRoot}"))).GetProperty("name").GetString());
    }

    // --- Modificación y eliminación ---

    [Fact]
    public async Task A_space_is_renamed_and_marked_common()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var root = await RootAsync(admin, site);
        var area = await SpaceAsync(admin, site, root, "zone", "Planta baja");

        var response = await admin.PutAsJsonAsync($"/api/v1/spaces/{area}", new { name = "SUM", type = "common_area", isCommon = true });
        var body = await Json(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("SUM", "common_area", true, root),
            (body.GetProperty("name").GetString(), body.GetProperty("type").GetString(), body.GetProperty("isCommon").GetBoolean(),
                body.GetProperty("parentId").GetGuid()));
    }

    [Fact]
    public async Task The_type_rule_of_the_root_holds_on_update()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var root = await RootAsync(admin, site);
        var tower = await SpaceAsync(admin, site, root, "building", "Torre");

        var rootToBuilding = await admin.PutAsJsonAsync($"/api/v1/spaces/{root}", new { name = "Edificio", type = "building" });
        var towerToSite = await admin.PutAsJsonAsync($"/api/v1/spaces/{tower}", new { name = "Torre", type = "site" });

        Assert.Equal(HttpStatusCode.BadRequest, rootToBuilding.StatusCode);
        Assert.True((await Json(rootToBuilding)).GetProperty("errors").TryGetProperty("Type", out _));
        Assert.Equal(HttpStatusCode.BadRequest, towerToSite.StatusCode);
        Assert.True((await Json(towerToSite)).GetProperty("errors").TryGetProperty("Type", out _));
    }

    [Fact]
    public async Task A_leaf_space_is_deleted()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var root = await RootAsync(admin, site);
        var tower = await SpaceAsync(admin, site, root, "building", "Torre");

        var response = await admin.DeleteAsync($"/api/v1/spaces/{tower}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/spaces/{tower}")).StatusCode);
    }

    [Fact]
    public async Task A_space_with_children_or_access_points_is_kept()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var root = await RootAsync(admin, site);
        var tower = await SpaceAsync(admin, site, root, "building", "Torre");
        await SpaceAsync(admin, site, tower, "floor", "Piso 1");
        var pool = await SpaceAsync(admin, site, root, "common_area", "Pileta", isCommon: true);
        await AccessPointApiTests.PointAsync(admin, site, "Puerta de la pileta", pool);

        var withChildren = await admin.DeleteAsync($"/api/v1/spaces/{tower}");
        var withPoint = await admin.DeleteAsync($"/api/v1/spaces/{pool}");

        Assert.Equal(HttpStatusCode.Conflict, withChildren.StatusCode);
        Assert.Equal("space_has_dependents", (await Json(withChildren)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, withPoint.StatusCode);
        Assert.Equal("space_has_dependents", (await Json(withPoint)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/spaces/{tower}")).StatusCode);
    }

    // --- Permisos y alcance por sitio ---

    [Fact]
    public async Task Reading_spaces_without_permission_responds_403()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var site = await SiteAsync(admin);
        var resident = await factory.CreateUserAsync(organization, "resident");
        using var client = factory.CreateClientWithToken(resident.Tokens.AccessToken);

        var response = await client.GetAsync($"/api/v1/sites/{site}/spaces");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("permission_denied", (await Json(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_site_scoped_guard_reads_only_its_site_and_cannot_manage_it()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var siteA = await SiteAsync(admin, "A");
        var siteB = await SiteAsync(admin, "B");
        var rootA = await RootAsync(admin, siteA);
        var rootB = await RootAsync(admin, siteB);
        var guard = await factory.CreateUserAsync(organization, "guard", "site", siteA);
        using var client = factory.CreateClientWithToken(guard.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/sites/{siteA}/spaces")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/spaces/{rootA}")).StatusCode);

        var listB = await client.GetAsync($"/api/v1/sites/{siteB}/spaces");
        var getB = await client.GetAsync($"/api/v1/spaces/{rootB}");
        var missing = await client.GetAsync($"/api/v1/spaces/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, listB.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getB.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(getB));

        // Ve el sitio A pero no lo gestiona: 403 (y nada cambia).
        var create = await PostSpaceAsync(client, siteA, new { name = "Torre", type = "building", parentId = rootA });
        var update = await client.PutAsJsonAsync($"/api/v1/spaces/{rootA}", new { name = "Pisado", type = "site" });
        var delete = await client.DeleteAsync($"/api/v1/spaces/{rootA}");
        Assert.All([create, update, delete], r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        Assert.Single((await Json(await admin.GetAsync($"/api/v1/sites/{siteA}/spaces"))).EnumerateArray());
        Assert.Equal("Edificio", (await Json(await admin.GetAsync($"/api/v1/spaces/{rootA}"))).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Anonymous_callers_are_rejected()
    {
        using var factory = new NexgateFactory();
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/v1/sites/{Guid.NewGuid()}/spaces");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
