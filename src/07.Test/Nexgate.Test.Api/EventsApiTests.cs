using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using static Nexgate.Test.Api.DeviceFlows;
using static Nexgate.Test.Api.SpacesApiTests;

namespace Nexgate.Test.Api;

/// <summary>Escenarios de event-ingestion por HTTP: ingesta idempotente, descuento de usos y consulta.</summary>
public class EventsApiTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    /// <summary>Sitio con portón y hall, un sujeto con una tarjeta y un dispositivo reclamado que controla los dos puntos.</summary>
    private sealed record Setup(Guid Site, Guid Gate, Guid Hall, Guid Subject, Guid Credential, string Hash, ClaimedDevice Device);

    private static async Task<Guid> IdOf(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetGuid();
    }

    private static async Task<Setup> SetupAsync(NexgateFactory factory, HttpClient admin, string name = "Edificio")
    {
        var site = await SiteAsync(admin, name);
        var root = await RootAsync(admin, site);
        var tower = await SpaceAsync(admin, site, root, "building", "Torre A");
        var gate = await AccessPointApiTests.PointAsync(admin, site, "Portón", root, "gate");
        var hall = await AccessPointApiTests.PointAsync(admin, site, "Hall A", tower);
        var subject = await IdOf(await admin.PostAsJsonAsync($"/api/v1/sites/{site}/subjects", new { kind = "person", alias = "Ana" }));
        var credential = await IdOf(await admin.PostAsJsonAsync($"/api/v1/sites/{site}/credentials", new { subjectId = subject, type = "card", value = "00A1B2C3" }));
        var policy = await Json(await admin.GetAsync($"/api/v1/access-points/{gate}/policy"));
        var hash = policy.GetProperty("credentials")[0].GetProperty("hash").GetString()!;
        var device = await CreateAndClaimAsync(factory, admin, site);
        await AssignAsync(admin, device.Id, gate, hall);
        return new Setup(site, gate, hall, subject, credential, hash, device);
    }

    private static object Event(Guid point, string? credential, string decision = "allow", string reason = "allowed",
        DateTimeOffset? at = null, Guid? id = null, int? policyVersion = 1) =>
        new { id = id ?? Guid.NewGuid(), occurredAt = at ?? Now, accessPointId = point, credential, decision, reason, policyVersion };

    private static Task<HttpResponseMessage> SendAsync(HttpClient device, params object[] events) =>
        device.PostAsJsonAsync("/api/v1/device/events", new { events });

    private static async Task<(int Accepted, int Duplicates)> SentAsync(HttpClient device, params object[] events)
    {
        var response = await SendAsync(device, events);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        return (body.GetProperty("accepted").GetInt32(), body.GetProperty("duplicates").GetInt32());
    }

    private static async Task<List<JsonElement>> EventsAsync(HttpClient admin, Guid site, string query = "")
    {
        var response = await admin.GetAsync($"/api/v1/sites/{site}/access-events{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Json(response)).GetProperty("items").EnumerateArray().ToList();
    }

    private static async Task<int?> RemainingUsesAsync(HttpClient admin, Guid grant)
    {
        var body = await Json(await admin.GetAsync($"/api/v1/grants/{grant}"));
        return body.GetProperty("remainingUses").ValueKind == JsonValueKind.Null ? null : body.GetProperty("remainingUses").GetInt32();
    }

    private static async Task<Guid> GrantAsync(HttpClient admin, Setup s, int? maxUses, Guid? target = null) =>
        await IdOf(await admin.PostAsJsonAsync($"/api/v1/sites/{s.Site}/grants",
            new { subjectId = s.Subject, targetType = "access_point", targetId = target ?? s.Gate, effect = "allow", maxUses }));

    // --- Ingesta ---

    [Fact]
    public async Task Events_are_stored_with_their_credential_and_subject()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SetupAsync(factory, admin);
        using var device = factory.CreateDeviceClient(s.Device.Certificate);
        var unknownHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        var sent = await SentAsync(device,
            Event(s.Gate, s.Hash, at: Now.AddMinutes(-2)),
            Event(s.Hall, unknownHash, "deny", "credential_invalid", Now.AddMinutes(-1)));
        var events = await EventsAsync(admin, s.Site);

        Assert.Equal((2, 0), sent);
        Assert.Equal(2, events.Count);
        var denied = events[0];
        var allowed = events[1];
        Assert.Equal((s.Hall, "deny", "credential_invalid"), (denied.GetProperty("accessPointId").GetGuid(), denied.GetProperty("decision").GetString(),
            denied.GetProperty("reason").GetString()));
        Assert.Equal(JsonValueKind.Null, denied.GetProperty("credentialId").ValueKind);
        Assert.Equal(JsonValueKind.Null, denied.GetProperty("subjectId").ValueKind);
        Assert.Equal((s.Credential, s.Subject, s.Device.Id), (allowed.GetProperty("credentialId").GetGuid(), allowed.GetProperty("subjectId").GetGuid(),
            allowed.GetProperty("deviceId").GetGuid()));
        Assert.Equal(1, allowed.GetProperty("policyVersion").GetInt32());
        Assert.All(events, e => Assert.DoesNotContain(s.Hash, e.GetRawText()));
        Assert.All(events, e => Assert.DoesNotContain(unknownHash, e.GetRawText()));
    }

    [Fact]
    public async Task An_invalid_batch_stores_nothing()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreign = await SetupAsync(factory, other, "Ajeno");
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SetupAsync(factory, admin);
        var otherSite = await SiteAsync(admin, "Otro");
        var otherPoint = await AccessPointApiTests.PointAsync(admin, otherSite, "Portón", await RootAsync(admin, otherSite), "gate");
        using var device = factory.CreateDeviceClient(s.Device.Certificate);

        var empty = await SendAsync(device);
        var tooMany = await SendAsync(device, Enumerable.Range(0, 101).Select(_ => Event(s.Gate, null, "deny", "no_permission")).ToArray());
        var invalid = await SendAsync(device,
            Event(s.Gate, s.Hash),
            new { id = (Guid?)null, occurredAt = (DateTimeOffset?)null, accessPointId = s.Gate, decision = "allow", reason = "explicit_deny" },
            new { id = Guid.NewGuid(), occurredAt = Now, accessPointId = s.Gate, decision = "maybe", reason = "allowed" });
        var ofOtherSite = await SendAsync(device, Event(s.Gate, s.Hash), Event(otherPoint, null, "deny", "no_permission"));
        var ofOtherOrganization = await SendAsync(device, Event(s.Gate, s.Hash), Event(foreign.Gate, null, "deny", "no_permission"));
        var missing = await SendAsync(device, Event(s.Gate, s.Hash), Event(Guid.NewGuid(), null, "deny", "no_permission"));

        Assert.All([empty, tooMany, invalid, ofOtherSite, ofOtherOrganization, missing], r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        var errors = (await Json(invalid)).GetProperty("errors");
        var keys = errors.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains(keys, k => k.Contains("[1]") && k.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && !k.Contains("AccessPoint"));
        Assert.Contains(keys, k => k.Contains("[1]") && k.EndsWith("OccurredAt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(keys, k => k.Contains("[1]") && k.EndsWith("Reason", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(keys, k => k.Contains("[2]") && k.EndsWith("Decision", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(ofOtherSite));
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(ofOtherOrganization));
        Assert.Empty(await EventsAsync(admin, s.Site));
    }

    [Fact]
    public async Task Resending_a_batch_does_not_duplicate_events()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SetupAsync(factory, admin);
        using var device = factory.CreateDeviceClient(s.Device.Certificate);
        var first = Event(s.Gate, s.Hash);
        var second = Event(s.Hall, null, "deny", "no_permission");

        var once = await SentAsync(device, first, second);
        var twice = await SentAsync(device, first, second);
        var partial = await SentAsync(device, first, Event(s.Gate, s.Hash));

        Assert.Equal((2, 0), once);
        Assert.Equal((0, 2), twice);
        Assert.Equal((1, 1), partial);
        Assert.Equal(3, (await EventsAsync(admin, s.Site)).Count);
    }

    [Fact]
    public async Task Events_recorded_offline_keep_their_instant()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SetupAsync(factory, admin);
        using var device = factory.CreateDeviceClient(s.Device.Certificate);
        var fiveHoursAgo = Now.AddHours(-5);
        var fourHoursAgo = Now.AddHours(-4);

        await SentAsync(device, Event(s.Gate, s.Hash, at: fourHoursAgo), Event(s.Gate, s.Hash, at: fiveHoursAgo));
        await SentAsync(device, Event(s.Hall, s.Hash, at: Now.AddMinutes(-1)));
        var events = await EventsAsync(admin, s.Site);

        Assert.Equal(3, events.Count);
        Assert.Equal(fiveHoursAgo.ToUnixTimeMilliseconds(), events[2].GetProperty("occurredAt").GetDateTimeOffset().ToUnixTimeMilliseconds());
        Assert.True(events[2].GetProperty("receivedAt").GetDateTimeOffset() > Now.AddMinutes(-1));
        Assert.True(events.Select(e => e.GetProperty("occurredAt").GetDateTimeOffset()).SequenceEqual(
            events.Select(e => e.GetProperty("occurredAt").GetDateTimeOffset()).OrderDescending()));
    }

    // --- Descuento de usos ---

    [Fact]
    public async Task An_allowed_event_consumes_a_use_and_the_policy_gets_a_new_version()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SetupAsync(factory, admin);
        var grant = await GrantAsync(admin, s, 1);
        using var device = factory.CreateDeviceClient(s.Device.Certificate);
        var before = await Json(await device.GetAsync("/api/v1/device/policy"));

        var allowed = Event(s.Gate, s.Hash);
        await SentAsync(device, allowed);
        var afterFirst = await RemainingUsesAsync(admin, grant);
        await SentAsync(device, allowed);
        await SentAsync(device, Event(s.Gate, s.Hash, "deny", "no_permission"));
        var after = await Json(await device.GetAsync("/api/v1/device/policy"));

        Assert.Equal(0, afterFirst);
        Assert.Equal(0, await RemainingUsesAsync(admin, grant));
        Assert.Equal(before.GetProperty("version").GetInt32() + 1, after.GetProperty("version").GetInt32());
        var gatePolicy = after.GetProperty("points").EnumerateArray().Single(p => p.GetProperty("accessPointId").GetGuid() == s.Gate);
        Assert.Equal(0, gatePolicy.GetProperty("entries")[0].GetProperty("uses").GetInt32());
    }

    [Fact]
    public async Task No_use_is_consumed_when_an_unlimited_grant_also_applies()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SetupAsync(factory, admin);
        var limited = await GrantAsync(admin, s, 3);
        await GrantAsync(admin, s, null);
        using var device = factory.CreateDeviceClient(s.Device.Certificate);

        await SentAsync(device, Event(s.Gate, s.Hash));

        Assert.Equal(3, await RemainingUsesAsync(admin, limited));
    }

    [Fact]
    public async Task Two_offline_devices_never_take_the_uses_below_zero()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SetupAsync(factory, admin);
        var grant = await GrantAsync(admin, s, 1);
        var second = await CreateAndClaimAsync(factory, admin, s.Site, "Segundo");
        await AssignAsync(admin, s.Device.Id, s.Gate);
        await AssignAsync(admin, second.Id, s.Hall);
        using var first = factory.CreateDeviceClient(s.Device.Certificate);
        using var other = factory.CreateDeviceClient(second.Certificate);

        await SentAsync(first, Event(s.Gate, s.Hash, at: Now.AddHours(-2)));
        await SentAsync(other, Event(s.Gate, s.Hash, at: Now.AddHours(-1)));

        Assert.Equal(0, await RemainingUsesAsync(admin, grant));
        Assert.Equal(2, (await EventsAsync(admin, s.Site)).Count);
    }

    [Fact]
    public async Task A_resent_event_completes_a_pending_use_consumption()
    {
        var db = await TestDatabase.GetAsync();
        using var factory = new NexgateFactory(db);
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var s = await SetupAsync(factory, admin);
        var grant = await GrantAsync(admin, s, 2);
        using var device = factory.CreateDeviceClient(s.Device.Certificate);
        var allowed = Event(s.Gate, s.Hash);
        await SentAsync(device, allowed);

        // Simula una solicitud cortada entre el registro del evento y el descuento: el evento quedó pendiente.
        await using (var connection = await db.OpenAsAppAsync(organization))
        {
            await using var pending = new NpgsqlCommand("UPDATE events.access_event SET uses_settled = false WHERE device_id = @device", connection);
            pending.Parameters.AddWithValue("device", s.Device.Id);
            Assert.Equal(1, await pending.ExecuteNonQueryAsync());
            await using var restore = new NpgsqlCommand("UPDATE access.\"grant\" SET remaining_uses = 2 WHERE id = @grant", connection);
            restore.Parameters.AddWithValue("grant", grant);
            Assert.Equal(1, await restore.ExecuteNonQueryAsync());
        }

        var resent = await SentAsync(device, allowed);
        var again = await SentAsync(device, allowed);

        Assert.Equal((0, 1), resent);
        Assert.Equal((0, 1), again);
        Assert.Equal(1, await RemainingUsesAsync(admin, grant));
    }

    // --- Consulta ---

    [Fact]
    public async Task Events_are_filtered_and_paged_with_a_cursor()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SetupAsync(factory, admin);
        using var device = factory.CreateDeviceClient(s.Device.Certificate);
        var events = Enumerable.Range(1, 7).Select(i => Event(i % 2 == 0 ? s.Hall : s.Gate, null, "deny", "no_permission", Now.AddMinutes(-i))).ToArray();
        await SentAsync(device, events);

        var firstPage = await Json(await admin.GetAsync($"/api/v1/sites/{s.Site}/access-events?limit=3"));
        var cursor = firstPage.GetProperty("nextCursor").GetString()!;
        var secondPage = await Json(await admin.GetAsync($"/api/v1/sites/{s.Site}/access-events?limit=3&cursor={Uri.EscapeDataString(cursor)}"));
        var thirdPage = await Json(await admin.GetAsync(
            $"/api/v1/sites/{s.Site}/access-events?limit=3&cursor={Uri.EscapeDataString(secondPage.GetProperty("nextCursor").GetString()!)}"));
        var from = Uri.EscapeDataString(Now.AddMinutes(-5.5).ToString("O"));
        var to = Uri.EscapeDataString(Now.AddMinutes(-1.5).ToString("O"));
        var filtered = await EventsAsync(admin, s.Site, $"?accessPointId={s.Gate}&from={from}&to={to}");
        var badCursor = await admin.GetAsync($"/api/v1/sites/{s.Site}/access-events?cursor=xyz");

        var all = new[] { firstPage, secondPage, thirdPage }.SelectMany(p => p.GetProperty("items").EnumerateArray())
            .Select(e => e.GetProperty("occurredAt").GetDateTimeOffset()).ToList();
        Assert.Equal(7, all.Count);
        Assert.Equal(7, all.Distinct().Count());
        Assert.True(all.SequenceEqual(all.OrderDescending()));
        Assert.Equal(JsonValueKind.Null, thirdPage.GetProperty("nextCursor").ValueKind);
        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, e => Assert.Equal(s.Gate, e.GetProperty("accessPointId").GetGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, badCursor.StatusCode);
        Assert.Empty(await EventsAsync(admin, s.Site, $"?subjectId={Guid.NewGuid()}"));
    }

    [Fact]
    public async Task Event_permissions_and_sites_out_of_reach()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreignSite = await SiteAsync(other);
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var s = await SetupAsync(factory, admin);
        var guard = await factory.CreateUserAsync(organization, "guard", "site", s.Site);
        var resident = await factory.CreateUserAsync(organization, "resident");
        using var guardClient = factory.CreateClientWithToken(guard.Tokens.AccessToken);
        using var residentClient = factory.CreateClientWithToken(resident.Tokens.AccessToken);

        var foreign = await admin.GetAsync($"/api/v1/sites/{foreignSite}/access-events");
        var missing = await admin.GetAsync($"/api/v1/sites/{Guid.NewGuid()}/access-events");

        Assert.Equal(HttpStatusCode.OK, (await guardClient.GetAsync($"/api/v1/sites/{s.Site}/access-events")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await residentClient.GetAsync($"/api/v1/sites/{s.Site}/access-events")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(foreign));
    }

    // --- Privacidad y aislamiento ---

    [Fact]
    public async Task Credential_hashes_of_events_are_not_logged()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SetupAsync(factory, admin);
        using var device = factory.CreateDeviceClient(s.Device.Certificate);
        var unknownHash = "ZZZZtestUnknownHashValue0123456789abcdefghi";

        await SentAsync(device, Event(s.Gate, s.Hash), Event(s.Gate, unknownHash, "deny", "credential_invalid"));
        await SendAsync(device, Event(Guid.NewGuid(), unknownHash, "deny", "credential_invalid"));

        var text = string.Join("\n", factory.Logs.Entries.Select(e => e.AllText));
        Assert.DoesNotContain(s.Hash, text);
        Assert.DoesNotContain(unknownHash, text);
    }

    [Fact]
    public async Task The_events_table_is_isolated_by_organization()
    {
        var db = await TestDatabase.GetAsync();
        using var factory = new NexgateFactory(db);
        async Task<Guid> Tenant(string name)
        {
            var organization = await factory.CreateOrganizationAsync(name);
            using var client = factory.CreateClientFor(organization);
            var s = await SetupAsync(factory, client, $"Sitio {name}");
            using var device = factory.CreateDeviceClient(s.Device.Certificate);
            await SentAsync(device, Event(s.Gate, s.Hash));
            return organization;
        }

        var a = await Tenant("A");
        var b = await Tenant("B");
        await using var connection = await db.OpenAsAppAsync(a);

        await using (var select = new NpgsqlCommand("SELECT organization_id FROM events.access_event", connection))
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

        await using var move = new NpgsqlCommand("UPDATE events.access_event SET organization_id = @other", connection);
        move.Parameters.AddWithValue("other", b);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => move.ExecuteNonQueryAsync())).SqlState);
    }
}
