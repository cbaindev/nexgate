using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nexgate.Common.BuildingBlocks.Security;
using static Nexgate.Test.Api.SpacesApiTests;

namespace Nexgate.Test.Api;

/// <summary>Escenarios de roles con alcance de espacio (cambio add-space-scoped-roles).</summary>
public class SpaceScopeTests
{
    /// <summary>Sitio con raíz, torre A (piso 5, unidad 5B) y torre B, con un portón a la raíz y una puerta por espacio.</summary>
    private sealed record Building(Guid Site, Guid Root, Guid TowerA, Guid Floor5, Guid Unit5B, Guid TowerB,
        Guid Gate, Guid HallA, Guid FloorDoor, Guid HallB);

    private static async Task<Building> BuildingAsync(HttpClient admin, string name = "Edificio")
    {
        var site = await SiteAsync(admin, name);
        var root = await RootAsync(admin, site);
        var towerA = await SpaceAsync(admin, site, root, "building", "Torre A");
        var floor5 = await SpaceAsync(admin, site, towerA, "floor", "Piso 5");
        var unit = await SpaceAsync(admin, site, floor5, "unit", "5B");
        var towerB = await SpaceAsync(admin, site, root, "building", "Torre B");
        var gate = await AccessPointApiTests.PointAsync(admin, site, "Portón", root, "gate");
        var hallA = await AccessPointApiTests.PointAsync(admin, site, "Hall A", towerA);
        var floorDoor = await AccessPointApiTests.PointAsync(admin, site, "Puerta del piso 5", floor5);
        var hallB = await AccessPointApiTests.PointAsync(admin, site, "Hall B", towerB);
        return new Building(site, root, towerA, floor5, unit, towerB, gate, hallA, floorDoor, hallB);
    }

    private static async Task<List<string>> NamesAsync(HttpClient client, string path) =>
        (await Json(await client.GetAsync(path))).EnumerateArray().Select(e => e.GetProperty("name").GetString()!).ToList();

    // --- Unitarias del alcance ---

    [Fact]
    public void A_space_scope_covers_its_subtree_and_reaches_its_site()
    {
        var site = Guid.NewGuid();
        var otherSite = Guid.NewGuid();
        var (root, tower, floor, sibling) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var context = new RequestContext();
        context.SetOrganization(Guid.NewGuid(),
        [
            new PermissionGrant("spaces.read", site, tower),
            new PermissionGrant("spaces.read", otherSite)
        ]);
        var scope = new CurrentPermissions(context).ScopeFor("spaces.read");

        Assert.False(scope.Organization);
        Assert.True(scope.ReachesSite(site));
        Assert.False(scope.CoversSite(site));
        Assert.True(scope.CoversSite(otherSite));
        Assert.True(scope.Covers(site, [root, tower]));
        Assert.True(scope.Covers(site, [root, tower, floor]));
        Assert.False(scope.Covers(site, [root]));
        Assert.False(scope.Covers(site, [root, sibling]));
        Assert.True(scope.Covers(otherSite, [Guid.NewGuid()]));
        Assert.False(new CurrentPermissions(context).SitesFor("spaces.read").Allows(site));
        Assert.False(new CurrentPermissions(context).ScopeFor("spaces.manage").Any);
    }

    // --- Asignación e invitación ---

    [Fact]
    public async Task A_role_is_assigned_with_space_scope()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var b = await BuildingAsync(admin);
        var member = await factory.CreateUserAsync(organization, "resident");

        var response = await admin.PostAsJsonAsync($"/api/v1/members/{member.UserId}/role-assignments",
            new { role = "guard", scopeType = "space", spaceId = b.TowerA });
        var body = await Json(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("space", body.GetProperty("scopeType").GetString());
        Assert.Equal(b.TowerA, body.GetProperty("spaceId").GetGuid());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("siteId").ValueKind);
    }

    [Fact]
    public async Task A_space_of_another_organization_or_missing_is_rejected_the_same()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreign = await RootAsync(other, await SiteAsync(other));
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var member = await factory.CreateUserAsync(organization, "resident");

        var foreignAssign = await admin.PostAsJsonAsync($"/api/v1/members/{member.UserId}/role-assignments",
            new { role = "guard", scopeType = "space", spaceId = foreign });
        var missingAssign = await admin.PostAsJsonAsync($"/api/v1/members/{member.UserId}/role-assignments",
            new { role = "guard", scopeType = "space", spaceId = Guid.NewGuid() });
        var foreignInvite = await admin.PostAsJsonAsync("/api/v1/invitations",
            new { email = NexgateFactory.NewEmail(), role = "guard", scopeType = "space", spaceId = foreign });

        Assert.Equal(HttpStatusCode.BadRequest, foreignAssign.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(missingAssign), await WithoutPerRequestFields(foreignAssign));
        Assert.True((await Json(foreignAssign)).GetProperty("errors").TryGetProperty("SpaceId", out _));
        Assert.Equal(HttpStatusCode.BadRequest, foreignInvite.StatusCode);
        Assert.True((await Json(foreignInvite)).GetProperty("errors").TryGetProperty("SpaceId", out _));
    }

    [Fact]
    public async Task A_space_scope_requires_the_space_identifier()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var member = await factory.CreateUserAsync(organization, "resident");

        var response = await admin.PostAsJsonAsync($"/api/v1/members/{member.UserId}/role-assignments",
            new { role = "guard", scopeType = "space" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("SpaceId", out _));
    }

    [Fact]
    public async Task An_invitation_with_space_scope_gives_that_scope_and_its_permissions()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var b = await BuildingAsync(admin);

        var invitation = await factory.InviteAsync(organization, NexgateFactory.NewEmail(), "guard", "space", spaceId: b.TowerA);
        var guard = await factory.AcceptAndSignInAsync(invitation.Token, invitation.Email, organization);
        using var client = factory.CreateClientWithToken(guard.Tokens.AccessToken);
        var permissions = (await Json(await client.GetAsync("/api/v1/me/permissions"))).GetProperty("permissions").EnumerateArray().ToList();

        Assert.Equal(("space", b.TowerA), (invitation.ScopeType, invitation.SpaceId));
        Assert.Equal(["organization.read", "sites.read", "spaces.read"], permissions.Select(p => p.GetProperty("name").GetString()));
        Assert.All(permissions, p =>
        {
            Assert.False(p.GetProperty("organization").GetBoolean());
            Assert.Empty(p.GetProperty("siteIds").EnumerateArray());
            var space = Assert.Single(p.GetProperty("spaces").EnumerateArray());
            Assert.Equal((b.TowerA, b.Site), (space.GetProperty("spaceId").GetGuid(), space.GetProperty("siteId").GetGuid()));
        });
    }

    // --- Guardia con alcance en la torre A ---

    [Fact]
    public async Task A_space_scoped_guard_sees_its_site_its_subtree_and_its_access_points()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var b = await BuildingAsync(admin);
        var otherSite = await SiteAsync(admin, "Otro");
        var guard = await factory.CreateUserAsync(organization, "guard", "space", spaceId: b.TowerA);
        using var client = factory.CreateClientWithToken(guard.Tokens.AccessToken);

        Assert.Equal(["Edificio"], await NamesAsync(client, "/api/v1/sites"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/sites/{b.Site}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/sites/{otherSite}")).StatusCode);

        Assert.Equal(["Torre A", "Piso 5", "5B"], await NamesAsync(client, $"/api/v1/sites/{b.Site}/spaces"));
        Assert.Equal(["Hall A", "Puerta del piso 5"], await NamesAsync(client, $"/api/v1/sites/{b.Site}/access-points"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/spaces/{b.Floor5}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/access-points/{b.FloorDoor}")).StatusCode);

        // El camino se devuelve completo, con el portón de la raíz.
        Assert.Equal(["Portón", "Hall A", "Puerta del piso 5"], await NamesAsync(client, $"/api/v1/spaces/{b.Unit5B}/access-path"));
    }

    [Fact]
    public async Task A_space_scoped_guard_does_not_see_ancestors_or_other_branches()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var b = await BuildingAsync(admin);
        var guard = await factory.CreateUserAsync(organization, "guard", "space", spaceId: b.TowerA);
        using var client = factory.CreateClientWithToken(guard.Tokens.AccessToken);
        var missing = await client.GetAsync($"/api/v1/spaces/{Guid.NewGuid()}");

        foreach (var path in new[]
                 {
                     $"/api/v1/spaces/{b.Root}", $"/api/v1/spaces/{b.TowerB}", $"/api/v1/spaces/{b.TowerB}/access-path",
                     $"/api/v1/access-points/{b.Gate}", $"/api/v1/access-points/{b.HallB}"
                 })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(response));
        }
    }

    // --- Administrador con alcance en la torre A ---

    [Fact]
    public async Task A_space_scoped_administrator_works_inside_its_subtree()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var b = await BuildingAsync(admin);
        var towerAdmin = await factory.CreateUserAsync(organization, "administrator", "space", spaceId: b.TowerA);
        using var client = factory.CreateClientWithToken(towerAdmin.Tokens.AccessToken);

        var floor6 = await SpaceAsync(client, b.Site, b.TowerA, "floor", "Piso 6");
        var rename = await client.PutAsJsonAsync($"/api/v1/spaces/{b.TowerA}", new { name = "Torre Norte", type = "building" });
        var door = await AccessPointApiTests.PointAsync(client, b.Site, "Puerta del piso 6", floor6, fromSpaceId: b.TowerA);
        // El origen puede estar fuera del subárbol: la puerta del hall se entra desde la raíz.
        await AccessPointApiTests.PointAsync(client, b.Site, "Hall A peatonal", b.TowerA, fromSpaceId: b.Root);
        var deletePoint = await client.DeleteAsync($"/api/v1/access-points/{door}");
        var deleteFloor = await client.DeleteAsync($"/api/v1/spaces/{floor6}");

        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deletePoint.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteFloor.StatusCode);
    }

    [Fact]
    public async Task A_space_scoped_administrator_cannot_reach_outside_its_subtree()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var b = await BuildingAsync(admin);
        var otherSite = await SiteAsync(admin, "Otro");
        var towerAdmin = await factory.CreateUserAsync(organization, "administrator", "space", spaceId: b.TowerA);
        using var client = factory.CreateClientWithToken(towerAdmin.Tokens.AccessToken);

        // Padre fuera del subárbol: igual que un padre inexistente.
        var underRoot = await PostSpaceAsync(client, b.Site, new { name = "Torre C", type = "building", parentId = b.Root });
        var missingParent = await PostSpaceAsync(client, b.Site, new { name = "Torre C", type = "building", parentId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, underRoot.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(missingParent), await WithoutPerRequestFields(underRoot));

        // Raíz: 403 en el sitio que ve, 404 en otro.
        Assert.Equal(HttpStatusCode.Forbidden, (await PostSpaceAsync(client, b.Site, new { name = "Raíz", type = "site" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostSpaceAsync(client, otherSite, new { name = "Raíz", type = "site" })).StatusCode);

        // Punto hacia un espacio fuera del subárbol, al crear y al mover uno propio.
        var outside = await AccessPointApiTests.PostPointAsync(client, b.Site,
            new { name = "Hall B 2", type = "door", toSpaceId = b.TowerB, failMode = "fail_safe" });
        var move = await client.PutAsJsonAsync($"/api/v1/access-points/{b.HallA}",
            new { name = "Hall A", type = "door", toSpaceId = b.Root, failMode = "fail_safe" });
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.True((await Json(outside)).GetProperty("errors").TryGetProperty("ToSpaceId", out _));
        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);

        // El sitio se ve pero no se modifica; miembros, invitaciones y alta de sitios son de organización.
        var updateSite = await client.PutAsJsonAsync($"/api/v1/sites/{b.Site}", new { name = "Pisado", timeZone = "America/Argentina/Cordoba" });
        var members = await client.GetAsync("/api/v1/members");
        var invite = await client.PostAsJsonAsync("/api/v1/invitations",
            new { email = NexgateFactory.NewEmail(), role = "guard", scopeType = "space", spaceId = b.TowerA });
        var createSite = await client.PostAsJsonAsync("/api/v1/sites", new { name = "Nuevo", type = "building", timeZone = "America/Argentina/Cordoba" });
        Assert.All([updateSite, members, invite, createSite], r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));

        Assert.Equal(["Edificio", "Otro"], await NamesAsync(admin, "/api/v1/sites"));
        Assert.Equal(b.TowerA, (await Json(await admin.GetAsync($"/api/v1/access-points/{b.HallA}"))).GetProperty("toSpaceId").GetGuid());
        Assert.Equal(6 - 1, (await Json(await admin.GetAsync($"/api/v1/sites/{b.Site}/spaces"))).GetArrayLength());
    }

    [Fact]
    public async Task Scopes_of_the_same_user_add_up()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var b = await BuildingAsync(admin);
        var siteB = await SiteAsync(admin, "Sitio B");
        var rootB = await RootAsync(admin, siteB);
        var siteC = await SiteAsync(admin, "Sitio C");
        var guard = await factory.CreateUserAsync(organization, "guard", "space", spaceId: b.TowerA);
        var assign = await admin.PostAsJsonAsync($"/api/v1/members/{guard.UserId}/role-assignments",
            new { role = "guard", scopeType = "site", siteId = siteB });
        Assert.Equal(HttpStatusCode.Created, assign.StatusCode);
        using var client = factory.CreateClientWithToken(guard.Tokens.AccessToken);

        Assert.Equal(["Edificio", "Sitio B"], await NamesAsync(client, "/api/v1/sites"));
        Assert.Equal(["Torre A", "Piso 5", "5B"], await NamesAsync(client, $"/api/v1/sites/{b.Site}/spaces"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/spaces/{rootB}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/sites/{siteC}/spaces")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/spaces/{b.TowerB}")).StatusCode);
    }

    // --- Eliminación de un espacio usado como alcance ---

    [Fact]
    public async Task A_space_used_as_a_role_scope_or_by_a_pending_invitation_is_kept()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var site = await SiteAsync(admin);
        var root = await RootAsync(admin, site);
        var assigned = await SpaceAsync(admin, site, root, "zone", "Asignada");
        var invited = await SpaceAsync(admin, site, root, "zone", "Invitada");
        var leaf = await SpaceAsync(admin, site, root, "zone", "Hoja");
        var guard = await factory.CreateUserAsync(organization, "guard", "space", spaceId: assigned);
        await factory.InviteAsync(organization, NexgateFactory.NewEmail(), "guard", "space", spaceId: invited);

        var withAssignment = await admin.DeleteAsync($"/api/v1/spaces/{assigned}");
        var withInvitation = await admin.DeleteAsync($"/api/v1/spaces/{invited}");
        var withChildren = await admin.DeleteAsync($"/api/v1/spaces/{root}");

        Assert.Equal(HttpStatusCode.Conflict, withAssignment.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, withInvitation.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(withChildren), await WithoutPerRequestFields(withAssignment));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/spaces/{leaf}")).StatusCode);

        // Al revocar la asignación, el espacio se puede eliminar.
        var members = await Json(await admin.GetAsync("/api/v1/members"));
        var assignment = members.EnumerateArray().Single(m => m.GetProperty("userId").GetGuid() == guard.UserId)
            .GetProperty("roleAssignments").EnumerateArray().Single();
        Assert.Equal(assigned, assignment.GetProperty("spaceId").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.DeleteAsync($"/api/v1/members/{guard.UserId}/role-assignments/{assignment.GetProperty("id").GetGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/spaces/{assigned}")).StatusCode);
    }

    [Fact]
    public async Task Spaces_expose_their_ancestors()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);

        var unit = await Json(await admin.GetAsync($"/api/v1/spaces/{b.Unit5B}"));
        var root = await Json(await admin.GetAsync($"/api/v1/spaces/{b.Root}"));

        Assert.Equal([b.Root, b.TowerA, b.Floor5], unit.GetProperty("ancestorIds").EnumerateArray().Select(e => e.GetGuid()));
        Assert.Empty(root.GetProperty("ancestorIds").EnumerateArray());
    }
}
