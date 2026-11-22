using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using static Nexgate.Test.Api.SpacesApiTests;

namespace Nexgate.Test.Api;

/// <summary>Escenarios de subjects, access-grants, policy-compilation y access-evaluation por HTTP.</summary>
public class AccessApiTests
{
    // Martes 24/11/2026 a las 10:00 en Córdoba (UTC-3).
    private static readonly DateTimeOffset Tuesday10 = new(2026, 11, 24, 13, 0, 0, TimeSpan.Zero);

    /// <summary>Sitio con raíz, torre A (piso 5, unidad 5B), torre B, pileta y una puerta por espacio.</summary>
    private sealed record Building(Guid Site, Guid Root, Guid TowerA, Guid Floor5, Guid Unit5B, Guid TowerB, Guid Pool,
        Guid Gate, Guid HallA, Guid FloorDoor, Guid UnitDoor, Guid HallB, Guid PoolDoor);

    private static async Task<Building> BuildingAsync(HttpClient admin, string name = "Edificio")
    {
        var site = await SiteAsync(admin, name);
        var root = await RootAsync(admin, site);
        var towerA = await SpaceAsync(admin, site, root, "building", "Torre A");
        var floor5 = await SpaceAsync(admin, site, towerA, "floor", "Piso 5");
        var unit = await SpaceAsync(admin, site, floor5, "unit", "5B");
        var towerB = await SpaceAsync(admin, site, root, "building", "Torre B");
        var pool = await SpaceAsync(admin, site, root, "common_area", "Pileta", isCommon: true);
        return new Building(site, root, towerA, floor5, unit, towerB, pool,
            await AccessPointApiTests.PointAsync(admin, site, "Portón", root, "gate"),
            await AccessPointApiTests.PointAsync(admin, site, "Hall A", towerA),
            await AccessPointApiTests.PointAsync(admin, site, "Puerta del piso 5", floor5),
            await AccessPointApiTests.PointAsync(admin, site, "Puerta del 5B", unit),
            await AccessPointApiTests.PointAsync(admin, site, "Hall B", towerB),
            await AccessPointApiTests.PointAsync(admin, site, "Puerta de la pileta", pool));
    }

    private static async Task<Guid> IdOf(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.Created)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> SubjectAsync(HttpClient client, Guid site, string alias = "Empleada del 5B") =>
        await IdOf(await client.PostAsJsonAsync($"/api/v1/sites/{site}/subjects", new { kind = "person", alias }));

    private static async Task<Guid> WeekdaysAsync(HttpClient client, Guid site, string name = "Lunes a viernes de 8 a 17") =>
        await IdOf(await client.PostAsJsonAsync($"/api/v1/sites/{site}/schedules", new
        {
            name,
            windows = new[] { new { days = new[] { "monday", "tuesday", "wednesday", "thursday", "friday" }, from = "08:00", to = "17:00" } }
        }));

    private static async Task<Guid> CardAsync(HttpClient client, Guid site, Guid subject, string value = "00A1B2C3") =>
        await IdOf(await client.PostAsJsonAsync($"/api/v1/sites/{site}/credentials", new { subjectId = subject, type = "card", value }));

    private static Task<HttpResponseMessage> PostGrantAsync(HttpClient client, Guid site, object body) =>
        client.PostAsJsonAsync($"/api/v1/sites/{site}/grants", body);

    private static async Task<Guid> GrantAsync(HttpClient client, Guid site, Guid subject, string targetType, Guid target,
        Guid? scheduleId = null, string effect = "allow", int? maxUses = null) =>
        await IdOf(await PostGrantAsync(client, site, new { subjectId = subject, targetType, targetId = target, scheduleId, effect, maxUses }));

    private static async Task<(string Decision, string Reason)> SimulateAsync(HttpClient client, Guid point, string value = "00A1B2C3",
        DateTimeOffset? at = null)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/access-points/{point}/simulations",
            new { credentialType = "card", credentialValue = value, at = at ?? Tuesday10 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        return (body.GetProperty("decision").GetString()!, body.GetProperty("reason").GetString()!);
    }

    // --- Configuración ---

    [Theory]
    [InlineData(null)]
    [InlineData(Nexgate.Service.Access.AccessOptions.DevelopmentCredentialKey)]
    [InlineData("Y29ydGE=")]
    public void Application_refuses_to_start_outside_development_without_a_proper_credential_key(string? key)
    {
        using var factory = new NexgateFactory
        {
            Environment = "Production",
            SigningKey = NexgateFactory.NewSigningKey(),
            CredentialKey = key
        };

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var validation = Assert.IsType<Microsoft.Extensions.Options.OptionsValidationException>(error.GetBaseException());
        Assert.Contains("Access:CredentialKey", validation.Message);
    }

    // --- Sujetos ---

    [Fact]
    public async Task Subjects_are_registered_listed_modified_and_deleted()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);

        var created = await admin.PostAsJsonAsync($"/api/v1/sites/{site}/subjects", new { kind = "anonymous", alias = "Delivery" });
        var body = await Json(created);
        var id = body.GetProperty("id").GetGuid();
        await SubjectAsync(admin, site, "ana");
        var update = await admin.PutAsJsonAsync($"/api/v1/subjects/{id}", new { kind = "anonymous", alias = "Delivery 14:32" });
        var list = (await Json(await admin.GetAsync($"/api/v1/sites/{site}/subjects"))).EnumerateArray().Select(s => s.GetProperty("alias").GetString());
        var delete = await admin.DeleteAsync($"/api/v1/subjects/{id}");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal((site, "anonymous", "Delivery"), (body.GetProperty("siteId").GetGuid(), body.GetProperty("kind").GetString(), body.GetProperty("alias").GetString()));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(["ana", "Delivery 14:32"], list);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/subjects/{id}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_subjects_and_sites_out_of_reach_are_rejected()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreignSite = await SiteAsync(other);
        var foreignSubject = await SubjectAsync(other, foreignSite);
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);

        var invalid = await admin.PostAsJsonAsync($"/api/v1/sites/{site}/subjects", new { kind = "robot", alias = new string('x', 101) });
        var foreign = await admin.PostAsJsonAsync($"/api/v1/sites/{foreignSite}/subjects", new { kind = "person", alias = "X" });
        var missing = await admin.PostAsJsonAsync($"/api/v1/sites/{Guid.NewGuid()}/subjects", new { kind = "person", alias = "X" });
        var foreignGet = await admin.GetAsync($"/api/v1/subjects/{foreignSubject}");
        var missingGet = await admin.GetAsync($"/api/v1/subjects/{Guid.NewGuid()}");

        var errors = (await Json(invalid)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Kind", out _) && errors.TryGetProperty("Alias", out _));
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(foreign));
        Assert.Equal(await WithoutPerRequestFields(missingGet), await WithoutPerRequestFields(foreignGet));
    }

    [Fact]
    public async Task Access_permissions_follow_role_and_site_scope()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var b = await BuildingAsync(admin);
        var siteB = await SiteAsync(admin, "Otro");
        var guard = await factory.CreateUserAsync(organization, "guard", "site", b.Site);
        var spaceGuard = await factory.CreateUserAsync(organization, "guard", "space", spaceId: b.TowerA);
        var resident = await factory.CreateUserAsync(organization, "resident");
        using var guardClient = factory.CreateClientWithToken(guard.Tokens.AccessToken);
        using var spaceClient = factory.CreateClientWithToken(spaceGuard.Tokens.AccessToken);
        using var residentClient = factory.CreateClientWithToken(resident.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await guardClient.GetAsync($"/api/v1/sites/{b.Site}/subjects")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guardClient.GetAsync($"/api/v1/access-points/{b.Gate}/policy")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guardClient.GetAsync($"/api/v1/sites/{siteB}/grants")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await guardClient.PostAsJsonAsync($"/api/v1/sites/{b.Site}/subjects", new { kind = "person", alias = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spaceClient.GetAsync($"/api/v1/sites/{b.Site}/subjects")).StatusCode);
        var denied = await residentClient.GetAsync($"/api/v1/sites/{b.Site}/subjects");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("permission_denied", (await Json(denied)).GetProperty("code").GetString());
    }

    // --- Horarios ---

    [Fact]
    public async Task Schedules_are_validated_and_unique_by_name()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var created = await admin.PostAsJsonAsync($"/api/v1/sites/{site}/schedules", new
        {
            name = "Noches",
            windows = new object[]
            {
                new { days = new[] { "monday" }, from = "22:00", to = "24:00" },
                new { days = new[] { "tuesday" }, from = "00:00", to = "06:00" }
            }
        });
        var body = await Json(created);

        var invalid = await admin.PostAsJsonAsync($"/api/v1/sites/{site}/schedules",
            new { name = "Mal", windows = new[] { new { days = new[] { "monday" }, from = "17:00", to = "08:00" } } });
        var duplicate = await admin.PostAsJsonAsync($"/api/v1/sites/{site}/schedules",
            new { name = "NOCHES", windows = new[] { new { days = new[] { "monday" }, from = "08:00", to = "09:00" } } });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var windows = body.GetProperty("windows").EnumerateArray().ToList();
        Assert.Equal(("22:00", "24:00"), (windows[0].GetProperty("from").GetString(), windows[0].GetProperty("to").GetString()));
        Assert.Equal(["tuesday"], windows[1].GetProperty("days").EnumerateArray().Select(d => d.GetString()));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True((await Json(invalid)).GetProperty("errors").TryGetProperty("Windows", out _));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("schedule_name_conflict", (await Json(duplicate)).GetProperty("code").GetString());
    }

    // --- Credenciales ---

    [Fact]
    public async Task Credentials_never_expose_their_value_and_are_unique_per_site()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var otherSite = await SiteAsync(admin, "Otro");
        var subject = await SubjectAsync(admin, site);
        var otherSubject = await SubjectAsync(admin, otherSite);

        var created = await admin.PostAsJsonAsync($"/api/v1/sites/{site}/credentials",
            new { subjectId = subject, type = "card", value = "00a1b2c3", validUntil = Tuesday10.AddYears(1) });
        var raw = await created.Content.ReadAsStringAsync();
        var duplicate = await admin.PostAsJsonAsync($"/api/v1/sites/{site}/credentials", new { subjectId = subject, type = "card", value = " 00A1B2C3 " });
        var sameInOtherSite = await admin.PostAsJsonAsync($"/api/v1/sites/{otherSite}/credentials",
            new { subjectId = otherSubject, type = "card", value = "00A1B2C3" });
        var list = await (await admin.GetAsync($"/api/v1/sites/{site}/credentials?subjectId={subject}")).Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.DoesNotContain("00a1b2c3", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("value", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("00a1b2c3", list, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("credential_conflict", (await Json(duplicate)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Created, sameInOtherSite.StatusCode);
    }

    [Fact]
    public async Task A_credential_needs_a_subject_of_its_site_and_a_valid_period()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreignSubject = await SubjectAsync(other, await SiteAsync(other));
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var otherSiteSubject = await SubjectAsync(admin, await SiteAsync(admin, "Otro"));

        var responses = new[]
        {
            await admin.PostAsJsonAsync($"/api/v1/sites/{site}/credentials", new { subjectId = otherSiteSubject, type = "card", value = "1" }),
            await admin.PostAsJsonAsync($"/api/v1/sites/{site}/credentials", new { subjectId = foreignSubject, type = "card", value = "1" }),
            await admin.PostAsJsonAsync($"/api/v1/sites/{site}/credentials", new { subjectId = Guid.NewGuid(), type = "card", value = "1" })
        };
        var period = await admin.PostAsJsonAsync($"/api/v1/sites/{site}/credentials",
            new { subjectId = await SubjectAsync(admin, site), type = "pin", value = "1234", validFrom = Tuesday10, validUntil = Tuesday10.AddDays(-1) });

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        Assert.Single((await Task.WhenAll(responses.Select(WithoutPerRequestFields))).Distinct());
        Assert.True((await Json(responses[0])).GetProperty("errors").TryGetProperty("SubjectId", out _));
        Assert.True((await Json(period)).GetProperty("errors").TryGetProperty("ValidUntil", out _));
    }

    // --- Otorgamientos ---

    [Fact]
    public async Task Grants_are_created_validated_and_deleted()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreignSpace = await RootAsync(other, await SiteAsync(other));
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        var otherSite = await SiteAsync(admin, "Otro");
        var subject = await SubjectAsync(admin, b.Site);
        var schedule = await WeekdaysAsync(admin, b.Site);
        var otherSchedule = await WeekdaysAsync(admin, otherSite);

        var created = await PostGrantAsync(admin, b.Site,
            new { subjectId = subject, targetType = "space", targetId = b.Unit5B, scheduleId = schedule, effect = "allow", maxUses = 5 });
        var body = await Json(created);
        var badTarget = await PostGrantAsync(admin, b.Site, new { subjectId = subject, targetType = "space", targetId = foreignSpace, effect = "allow" });
        var missingTarget = await PostGrantAsync(admin, b.Site, new { subjectId = subject, targetType = "space", targetId = Guid.NewGuid(), effect = "allow" });
        var badSchedule = await PostGrantAsync(admin, b.Site,
            new { subjectId = subject, targetType = "access_point", targetId = b.Gate, scheduleId = otherSchedule, effect = "allow" });
        var denyWithUses = await PostGrantAsync(admin, b.Site,
            new { subjectId = subject, targetType = "access_point", targetId = b.Gate, effect = "deny", maxUses = 1 });
        var delete = await admin.DeleteAsync($"/api/v1/grants/{body.GetProperty("id").GetGuid()}");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(("space", b.Unit5B, "allow", "manual", 5),
            (body.GetProperty("targetType").GetString(), body.GetProperty("targetId").GetGuid(), body.GetProperty("effect").GetString(),
                body.GetProperty("origin").GetString(), body.GetProperty("remainingUses").GetInt32()));
        Assert.Equal(HttpStatusCode.BadRequest, badTarget.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(missingTarget), await WithoutPerRequestFields(badTarget));
        Assert.True((await Json(badTarget)).GetProperty("errors").TryGetProperty("TargetId", out _));
        Assert.True((await Json(badSchedule)).GetProperty("errors").TryGetProperty("ScheduleId", out _));
        Assert.True((await Json(denyWithUses)).GetProperty("errors").TryGetProperty("MaxUses", out _));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    // --- Cobertura, política y simulación ---

    [Fact]
    public async Task A_grant_to_a_unit_opens_the_doors_of_its_path_within_its_schedule()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        var subject = await SubjectAsync(admin, b.Site);
        await CardAsync(admin, b.Site, subject);
        await GrantAsync(admin, b.Site, subject, "space", b.Unit5B, await WeekdaysAsync(admin, b.Site));

        foreach (var door in new[] { b.Gate, b.HallA, b.FloorDoor, b.UnitDoor })
        {
            Assert.Equal(("allow", "allowed"), await SimulateAsync(admin, door));
        }

        Assert.Equal(("deny", "no_permission"), await SimulateAsync(admin, b.HallB));
        Assert.Equal(("deny", "no_permission"), await SimulateAsync(admin, b.PoolDoor));
        Assert.Equal(("deny", "no_permission"), await SimulateAsync(admin, b.UnitDoor, at: Tuesday10.AddHours(8)));
        Assert.Equal(("deny", "credential_invalid"), await SimulateAsync(admin, b.UnitDoor, value: "FFFFFFFF"));
    }

    [Fact]
    public async Task A_grant_to_an_intermediate_space_or_a_point_covers_only_its_part()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        var tower = await SubjectAsync(admin, b.Site, "Torre");
        var pool = await SubjectAsync(admin, b.Site, "Pileta");
        await CardAsync(admin, b.Site, tower, "111");
        await CardAsync(admin, b.Site, pool, "222");
        await GrantAsync(admin, b.Site, tower, "space", b.TowerA);
        await GrantAsync(admin, b.Site, pool, "access_point", b.PoolDoor);

        Assert.Equal("allowed", (await SimulateAsync(admin, b.Gate, "111")).Reason);
        Assert.Equal("allowed", (await SimulateAsync(admin, b.HallA, "111")).Reason);
        Assert.Equal("no_permission", (await SimulateAsync(admin, b.FloorDoor, "111")).Reason);
        Assert.Equal("allowed", (await SimulateAsync(admin, b.PoolDoor, "222")).Reason);
        Assert.Equal("no_permission", (await SimulateAsync(admin, b.Gate, "222")).Reason);
    }

    [Fact]
    public async Task A_deny_grant_wins_over_a_wider_allow()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        var subject = await SubjectAsync(admin, b.Site);
        await CardAsync(admin, b.Site, subject);
        await GrantAsync(admin, b.Site, subject, "space", b.Root);
        await GrantAsync(admin, b.Site, subject, "access_point", b.PoolDoor, effect: "deny");

        Assert.Equal(("allow", "allowed"), await SimulateAsync(admin, b.Gate));
        Assert.Equal(("deny", "explicit_deny"), await SimulateAsync(admin, b.PoolDoor));
    }

    [Fact]
    public async Task The_policy_has_the_format_and_a_stable_hash()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        var subject = await SubjectAsync(admin, b.Site);
        await CardAsync(admin, b.Site, subject);
        await GrantAsync(admin, b.Site, subject, "space", b.Unit5B, await WeekdaysAsync(admin, b.Site));

        async Task<JsonElement> PolicyAsync(Guid point) => await Json(await admin.GetAsync($"/api/v1/access-points/{point}/policy"));
        var first = await PolicyAsync(b.UnitDoor);
        var second = await PolicyAsync(b.UnitDoor);
        var hallB = await PolicyAsync(b.HallB);
        var other = await SubjectAsync(admin, b.Site, "Otro");
        await CardAsync(admin, b.Site, other, "99999999");
        var beforeGrant = await PolicyAsync(b.UnitDoor);
        await GrantAsync(admin, b.Site, other, "space", b.TowerB);
        var afterUnrelated = await PolicyAsync(b.UnitDoor);
        var hallBAfter = await PolicyAsync(b.HallB);

        Assert.Equal(1, first.GetProperty("format").GetInt32());
        Assert.Equal("America/Argentina/Cordoba", first.GetProperty("timeZone").GetString());
        Assert.Equal(-10800, first.GetProperty("offsets")[0].GetProperty("seconds").GetInt32());
        Assert.Equal(31, first.GetProperty("windows")[0][0].GetProperty("days").GetInt32());
        Assert.Single(first.GetProperty("credentials").EnumerateArray());
        Assert.DoesNotContain("00A1B2C3", first.ToString());
        Assert.Empty(hallB.GetProperty("entries").EnumerateArray());
        Assert.Equal(first.GetProperty("hash").GetString(), second.GetProperty("hash").GetString());
        // Un otorgamiento que no cubre la puerta del 5B no cambia su hash; el de la torre B sí cambia el del hall B.
        Assert.Equal(beforeGrant.GetProperty("hash").GetString(), afterUnrelated.GetProperty("hash").GetString());
        Assert.NotEqual(hallB.GetProperty("hash").GetString(), hallBAfter.GetProperty("hash").GetString());
    }

    [Fact]
    public async Task Deleting_a_credential_or_a_grant_closes_the_door()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        var subject = await SubjectAsync(admin, b.Site);
        var card = await CardAsync(admin, b.Site, subject);
        var grant = await GrantAsync(admin, b.Site, subject, "access_point", b.Gate);
        Assert.Equal("allowed", (await SimulateAsync(admin, b.Gate)).Reason);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/grants/{grant}")).StatusCode);
        Assert.Equal("no_permission", (await SimulateAsync(admin, b.Gate)).Reason);
        await GrantAsync(admin, b.Site, subject, "access_point", b.Gate);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/credentials/{card}")).StatusCode);
        Assert.Equal("credential_invalid", (await SimulateAsync(admin, b.Gate)).Reason);
    }

    [Fact]
    public async Task Policy_and_simulation_out_of_reach_respond_404()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreign = await BuildingAsync(other);
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());

        var missing = await admin.GetAsync($"/api/v1/access-points/{Guid.NewGuid()}/policy");
        var foreignPolicy = await admin.GetAsync($"/api/v1/access-points/{foreign.Gate}/policy");
        var foreignSimulation = await admin.PostAsJsonAsync($"/api/v1/access-points/{foreign.Gate}/simulations",
            new { credentialType = "card", credentialValue = "1" });

        Assert.Equal(HttpStatusCode.NotFound, foreignPolicy.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(foreignPolicy));
        Assert.Equal(HttpStatusCode.NotFound, foreignSimulation.StatusCode);
    }

    // --- Integridad entre módulos ---

    [Fact]
    public async Task Targets_schedules_and_subjects_in_use_are_kept()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        var leaf = await SpaceAsync(admin, b.Site, b.Root, "zone", "Depósito");
        var subject = await SubjectAsync(admin, b.Site);
        var schedule = await WeekdaysAsync(admin, b.Site);
        await GrantAsync(admin, b.Site, subject, "space", leaf, schedule);
        await GrantAsync(admin, b.Site, subject, "access_point", b.HallB);

        var space = await admin.DeleteAsync($"/api/v1/spaces/{leaf}");
        var point = await admin.DeleteAsync($"/api/v1/access-points/{b.HallB}");
        var scheduleDelete = await admin.DeleteAsync($"/api/v1/schedules/{schedule}");
        var subjectDelete = await admin.DeleteAsync($"/api/v1/subjects/{subject}");

        Assert.Equal("space_has_dependents", (await Json(space)).GetProperty("code").GetString());
        Assert.Equal("access_point_in_use", (await Json(point)).GetProperty("code").GetString());
        Assert.Equal("schedule_in_use", (await Json(scheduleDelete)).GetProperty("code").GetString());
        Assert.Equal("subject_in_use", (await Json(subjectDelete)).GetProperty("code").GetString());
        Assert.All([space, point, scheduleDelete, subjectDelete], r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    // --- Privacidad ---

    [Fact]
    public async Task Credential_values_hashes_and_aliases_are_not_logged()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var b = await BuildingAsync(admin);
        var subject = await SubjectAsync(admin, b.Site, "Alias-Secreto-Zeta");
        await CardAsync(admin, b.Site, subject, "C0FFEE99");
        await GrantAsync(admin, b.Site, subject, "access_point", b.Gate);
        var policy = await Json(await admin.GetAsync($"/api/v1/access-points/{b.Gate}/policy"));
        var hash = policy.GetProperty("credentials")[0].GetProperty("hash").GetString()!;
        await SimulateAsync(admin, b.Gate, "C0FFEE99");
        await SimulateAsync(admin, b.Gate, "BADBAD77");

        var text = string.Join("\n", factory.Logs.Entries.Select(e => e.AllText));
        Assert.DoesNotContain("C0FFEE99", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BADBAD77", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(hash, text);
        Assert.DoesNotContain("Alias-Secreto-Zeta", text);
    }

    // --- Aislamiento en la base ---

    [Theory]
    [InlineData("occupancy.subject")]
    [InlineData("access.schedule")]
    [InlineData("access.credential")]
    [InlineData("access.\"grant\"")]
    public async Task Access_tables_are_isolated_by_organization(string table)
    {
        var db = await TestDatabase.GetAsync();
        using var factory = new NexgateFactory(db);
        async Task<Guid> Tenant(string name)
        {
            var organization = await factory.CreateOrganizationAsync(name);
            using var client = factory.CreateClientFor(organization);
            var site = await SiteAsync(client, $"Sitio {name}");
            var root = await RootAsync(client, site);
            var subject = await SubjectAsync(client, site);
            await CardAsync(client, site, subject);
            await GrantAsync(client, site, subject, "space", root, await WeekdaysAsync(client, site));
            return organization;
        }

        var a = await Tenant("A");
        var b = await Tenant("B");
        await using var connection = await db.OpenAsAppAsync(a);

        await using (var select = new NpgsqlCommand($"SELECT organization_id FROM {table}", connection))
        await using (var reader = await select.ExecuteReaderAsync())
        {
            var organizations = new List<Guid>();
            while (await reader.ReadAsync())
            {
                organizations.Add(reader.GetGuid(0));
            }

            Assert.NotEmpty(organizations);
            Assert.All(organizations, id => Assert.Equal(a, id));
        }

        await using var move = new NpgsqlCommand($"UPDATE {table} SET organization_id = @other", connection);
        move.Parameters.AddWithValue("other", b);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => move.ExecuteNonQueryAsync())).SqlState);
    }
}
