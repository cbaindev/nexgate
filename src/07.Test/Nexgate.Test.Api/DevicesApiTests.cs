using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using static Nexgate.Test.Api.DeviceFlows;
using static Nexgate.Test.Api.SpacesApiTests;

namespace Nexgate.Test.Api;

/// <summary>Escenarios de device-provisioning y policy-sync por HTTP.</summary>
public class DevicesApiTests
{
    private sealed record Site(Guid Id, Guid Root, Guid Gate, Guid Hall);

    private static async Task<Site> SiteWithPointsAsync(HttpClient admin, string name = "Edificio")
    {
        var site = await SiteAsync(admin, name);
        var root = await RootAsync(admin, site);
        var hallSpace = await SpaceAsync(admin, site, root, "building", "Torre A");
        return new Site(site, root,
            await AccessPointApiTests.PointAsync(admin, site, "Portón", root, "gate"),
            await AccessPointApiTests.PointAsync(admin, site, "Hall A", hallSpace));
    }

    private static async Task<JsonElement> DeviceAsync(HttpClient admin, Guid id) => await Json(await admin.GetAsync($"/api/v1/devices/{id}"));

    private static async Task<HttpResponseMessage> PolicyAsync(HttpClient device, int? knownVersion = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/device/policy");
        if (knownVersion is { } v)
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{v}\""));
        }

        return await device.SendAsync(request);
    }

    private static async Task<(JsonElement Package, byte[] Bytes, string Signature)> PackageAsync(HttpClient device)
    {
        var response = await PolicyAsync(device);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return (JsonDocument.Parse(bytes).RootElement.Clone(), bytes, response.Headers.GetValues("X-Nexgate-Signature").Single());
    }

    internal static bool Verify(string publicKeyPem, byte[] content, string signature)
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(publicKeyPem);
        var s = signature.Replace('-', '+').Replace('_', '/');
        var raw = Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
        return key.VerifyData(content, raw, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    // --- Configuración ---

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Application_refuses_to_start_outside_development_without_proper_device_keys(bool authority, bool developmentKeys)
    {
        using var factory = new NexgateFactory
        {
            Environment = "Production",
            SigningKey = NexgateFactory.NewSigningKey(),
            DeviceAuthority = !authority ? null
                : developmentKeys
                    ? new DeviceKeys.Authority(Nexgate.Service.Devices.DevicesOptions.DevelopmentAuthorityCertificate,
                        Nexgate.Service.Devices.DevicesOptions.DevelopmentAuthorityKey, null!)
                    : DeviceKeys.TestAuthority,
            PolicySigningKey = authority && !developmentKeys ? null : Nexgate.Service.Devices.DevicesOptions.DevelopmentPolicySigningKey
        };

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var validation = Assert.IsType<Microsoft.Extensions.Options.OptionsValidationException>(error.GetBaseException());
        Assert.Contains("Devices:", validation.Message);
    }

    [Fact]
    public void Application_starts_outside_development_with_its_own_device_keys()
    {
        using var factory = new NexgateFactory { Environment = "Production", SigningKey = NexgateFactory.NewSigningKey() };

        using var client = factory.CreateClient();
    }

    // --- Alta y gestión ---

    [Fact]
    public async Task Devices_are_registered_with_a_claim_code_shown_once()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);

        var created = await admin.PostAsJsonAsync($"/api/v1/sites/{site}/devices", new { name = "Controlador portón" });
        var body = await Json(created);
        var id = body.GetProperty("device").GetProperty("id").GetGuid();
        await CreateDeviceAsync(admin, site, "a-controlador hall");
        var rename = await admin.PutAsJsonAsync($"/api/v1/devices/{id}", new { name = "Portón principal" });
        var get = await admin.GetAsync($"/api/v1/devices/{id}");
        var list = await admin.GetAsync($"/api/v1/sites/{site}/devices");
        var texts = new[] { await rename.Content.ReadAsStringAsync(), await get.Content.ReadAsStringAsync(), await list.Content.ReadAsStringAsync() };
        var names = (await Json(list)).EnumerateArray().Select(d => d.GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var device = body.GetProperty("device");
        Assert.Equal((site, "pending"), (device.GetProperty("siteId").GetGuid(), device.GetProperty("status").GetString()));
        Assert.Matches("^[2-9A-HJKMNP-Z]{5}-[2-9A-HJKMNP-Z]{5}$", body.GetProperty("claimCode").GetString());
        var expires = body.GetProperty("claimExpiresAt").GetDateTimeOffset();
        Assert.InRange(expires, DateTimeOffset.UtcNow.AddDays(7).AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(7).AddMinutes(5));
        Assert.All(texts, t => Assert.DoesNotContain(body.GetProperty("claimCode").GetString()!, t));
        Assert.All(texts, t => Assert.DoesNotContain("claimCode", t));
        Assert.Equal(["a-controlador hall", "Portón principal"], names);
    }

    [Fact]
    public async Task Invalid_devices_duplicated_names_and_sites_out_of_reach_are_rejected()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreignSite = await SiteAsync(other);
        var (foreignDevice, _) = await CreateDeviceAsync(other, foreignSite);
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        await CreateDeviceAsync(admin, site, "Portón");

        var empty = await admin.PostAsJsonAsync($"/api/v1/sites/{site}/devices", new { name = "  " });
        var tooLong = await admin.PostAsJsonAsync($"/api/v1/sites/{site}/devices", new { name = new string('x', 201) });
        var duplicated = await admin.PostAsJsonAsync($"/api/v1/sites/{site}/devices", new { name = "PORTÓN" });
        var foreign = await admin.PostAsJsonAsync($"/api/v1/sites/{foreignSite}/devices", new { name = "X" });
        var missing = await admin.PostAsJsonAsync($"/api/v1/sites/{Guid.NewGuid()}/devices", new { name = "X" });
        var foreignGet = await admin.GetAsync($"/api/v1/devices/{foreignDevice}");
        var foreignDelete = await admin.DeleteAsync($"/api/v1/devices/{foreignDevice}");
        var missingGet = await admin.GetAsync($"/api/v1/devices/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.True((await Json(tooLong)).GetProperty("errors").TryGetProperty("Name", out _));
        Assert.Equal(HttpStatusCode.Conflict, duplicated.StatusCode);
        Assert.Equal("device_name_conflict", (await Json(duplicated)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(foreign));
        Assert.Equal(await WithoutPerRequestFields(missingGet), await WithoutPerRequestFields(foreignGet));
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync($"/api/v1/devices/{foreignDevice}")).StatusCode);
    }

    [Fact]
    public async Task Access_points_are_assigned_to_one_device_of_the_same_site()
    {
        using var factory = new NexgateFactory();
        using var other = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreign = await SiteWithPointsAsync(other);
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SiteWithPointsAsync(admin);
        var otherSite = await SiteWithPointsAsync(admin, "Otro edificio");
        var (first, _) = await CreateDeviceAsync(admin, s.Id, "Uno");
        var (second, _) = await CreateDeviceAsync(admin, s.Id, "Dos");

        var assign = await admin.PutAsJsonAsync($"/api/v1/devices/{first}/access-points", new { accessPointIds = new[] { s.Gate, s.Hall } });
        var taken = await admin.PutAsJsonAsync($"/api/v1/devices/{second}/access-points", new { accessPointIds = new[] { s.Hall } });
        var ofOtherSite = await admin.PutAsJsonAsync($"/api/v1/devices/{second}/access-points", new { accessPointIds = new[] { otherSite.Gate } });
        var ofOtherOrganization = await admin.PutAsJsonAsync($"/api/v1/devices/{second}/access-points", new { accessPointIds = new[] { foreign.Gate } });
        var missing = await admin.PutAsJsonAsync($"/api/v1/devices/{second}/access-points", new { accessPointIds = new[] { Guid.NewGuid() } });
        var deletePoint = await admin.DeleteAsync($"/api/v1/access-points/{s.Gate}");
        var clear = await admin.PutAsJsonAsync($"/api/v1/devices/{first}/access-points", new { accessPointIds = Array.Empty<Guid>() });
        var reassign = await admin.PutAsJsonAsync($"/api/v1/devices/{second}/access-points", new { accessPointIds = new[] { s.Hall } });

        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        Assert.Equal(new[] { s.Gate, s.Hall }.Order(), (await Json(assign)).GetProperty("accessPointIds").EnumerateArray().Select(e => e.GetGuid()));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("access_point_assigned", (await Json(taken)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.True((await Json(missing)).GetProperty("errors").TryGetProperty("AccessPointIds", out _));
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(ofOtherSite));
        Assert.Equal(await WithoutPerRequestFields(missing), await WithoutPerRequestFields(ofOtherOrganization));
        Assert.Equal(HttpStatusCode.Conflict, deletePoint.StatusCode);
        Assert.Equal("access_point_in_use", (await Json(deletePoint)).GetProperty("code").GetString());
        Assert.Empty((await Json(clear)).GetProperty("accessPointIds").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, reassign.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/access-points/{s.Gate}")).StatusCode);
    }

    [Fact]
    public async Task Device_permissions_follow_role_and_site_scope()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        var a = await SiteWithPointsAsync(admin, "A");
        var b = await SiteWithPointsAsync(admin, "B");
        var (deviceB, _) = await CreateDeviceAsync(admin, b.Id);
        var guard = await factory.CreateUserAsync(organization, "guard", "site", a.Id);
        var spaceGuard = await factory.CreateUserAsync(organization, "guard", "space", spaceId: a.Root);
        var resident = await factory.CreateUserAsync(organization, "resident");
        using var guardClient = factory.CreateClientWithToken(guard.Tokens.AccessToken);
        using var spaceClient = factory.CreateClientWithToken(spaceGuard.Tokens.AccessToken);
        using var residentClient = factory.CreateClientWithToken(resident.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await guardClient.GetAsync($"/api/v1/sites/{a.Id}/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guardClient.PostAsJsonAsync($"/api/v1/sites/{a.Id}/devices", new { name = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guardClient.GetAsync($"/api/v1/devices/{deviceB}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spaceClient.GetAsync($"/api/v1/sites/{a.Id}/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await residentClient.GetAsync($"/api/v1/sites/{a.Id}/devices")).StatusCode);
    }

    // --- Reclamo ---

    [Fact]
    public async Task A_device_claims_its_identity_with_the_code_once()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var (id, code) = await CreateDeviceAsync(admin, site);

        var device = await ClaimAsync(factory, code.ToLowerInvariant().Replace("-", " "));
        var reused = await PostClaimAsync(factory, code, DeviceKeys.NewCsr().Csr);
        var unknown = await PostClaimAsync(factory, "ZZZZZ-ZZZZZ", DeviceKeys.NewCsr().Csr);
        var details = await DeviceAsync(admin, id);

        Assert.Equal((id, site), (device.Id, device.SiteId));
        Assert.Equal("CN=" + id.ToString("D"), device.Certificate.Subject);
        Assert.Contains(device.Certificate.Extensions.OfType<System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension>(),
            e => e.EnhancedKeyUsages.Cast<Oid>().Any(o => o.Value == "1.3.6.1.5.5.7.3.2"));
        Assert.Equal(DeviceKeys.TestAuthority.Certificate.Thumbprint,
            System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(device.Claim.GetProperty("authorityCertificate").GetString()!).Thumbprint);
        Assert.Equal(32, Convert.FromBase64String(device.CredentialKey).Length);
        Assert.Contains("BEGIN PUBLIC KEY", device.PolicyKey);
        Assert.Equal(60, device.Claim.GetProperty("pollSeconds").GetInt32());
        Assert.Equal("active", details.GetProperty("status").GetString());
        Assert.True(details.GetProperty("certificateExpiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddYears(2));
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        Assert.Equal("invalid_claim", (await Json(reused)).GetProperty("code").GetString());
        Assert.Equal(await WithoutPerRequestFields(unknown), await WithoutPerRequestFields(reused));
    }

    [Fact]
    public async Task An_expired_code_or_an_invalid_request_issues_no_certificate()
    {
        var db = await TestDatabase.GetAsync();
        using var factory = new NexgateFactory(db);
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var (expiredId, expiredCode) = await CreateDeviceAsync(admin, site, "Vencido");
        var (_, code) = await CreateDeviceAsync(admin, site, "Válido");
        await using (var connection = new NpgsqlConnection(db.OwnerConnectionString))
        {
            await connection.OpenAsync();
            await using var expire = new NpgsqlCommand("UPDATE devices.device SET claim_expires_at = now() - interval '1 minute' WHERE id = @id", connection);
            expire.Parameters.AddWithValue("id", expiredId);
            Assert.Equal(1, await expire.ExecuteNonQueryAsync());
        }

        using var rsa = RSA.Create(2048);
        var rsaCsr = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=x", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSigningRequestPem();
        var expired = await PostClaimAsync(factory, expiredCode, DeviceKeys.NewCsr().Csr);
        var unknown = await PostClaimAsync(factory, "ZZZZZ-ZZZZZ", DeviceKeys.NewCsr().Csr);
        var garbage = await PostClaimAsync(factory, code, "-----BEGIN CERTIFICATE REQUEST-----\nAAAA\n-----END CERTIFICATE REQUEST-----");
        var wrongCurve = await PostClaimAsync(factory, code, rsaCsr);
        var device = await ClaimAsync(factory, code);

        Assert.Equal(await WithoutPerRequestFields(unknown), await WithoutPerRequestFields(expired));
        Assert.All([garbage, wrongCurve], r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        Assert.True((await Json(garbage)).GetProperty("errors").TryGetProperty("CertificateRequest", out _));
        Assert.True((await Json(wrongCurve)).GetProperty("errors").TryGetProperty("CertificateRequest", out _));
        Assert.NotEqual(Guid.Empty, device.Id);
    }

    [Fact]
    public async Task Claim_attempts_are_limited_per_client()
    {
        using var factory = new NexgateFactory { ClaimPermits = 2 };
        var csr = DeviceKeys.NewCsr().Csr;

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++)
        {
            responses.Add(await PostClaimAsync(factory, "ZZZZZ-ZZZZZ", csr));
        }

        Assert.Equal([HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests], responses.Select(r => r.StatusCode));
    }

    // --- Autenticación ---

    [Fact]
    public async Task Only_the_current_certificate_of_an_active_device_is_accepted()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var device = await CreateAndClaimAsync(factory, admin, site);
        using var deviceClient = factory.CreateDeviceClient(device.Certificate);
        using var anonymous = factory.CreateClient();
        using var userOnDevice = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var foreignAuthority = DeviceKeys.NewAuthority("Otra autoridad");
        using var impostor = factory.CreateDeviceClient(DeviceKeys.ForeignClientCertificate(foreignAuthority, device.Certificate.Subject));

        Assert.Equal(HttpStatusCode.OK, (await deviceClient.GetAsync("/api/v1/device/policy")).StatusCode);
        var noCertificate = await anonymous.GetAsync("/api/v1/device/policy");
        Assert.Equal(HttpStatusCode.Unauthorized, noCertificate.StatusCode);
        Assert.Equal("device_authentication_required", (await Json(noCertificate)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await impostor.GetAsync("/api/v1/device/policy")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await userOnDevice.GetAsync("/api/v1/device/policy")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await deviceClient.GetAsync($"/api/v1/sites/{site}/devices")).StatusCode);
    }

    [Fact]
    public async Task Revoking_reissuing_the_code_or_deleting_invalidates_the_certificate()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SiteWithPointsAsync(admin);
        var revoked = await CreateAndClaimAsync(factory, admin, s.Id, "Revocado");
        var reissued = await CreateAndClaimAsync(factory, admin, s.Id, "Nuevo código");
        var deleted = await CreateAndClaimAsync(factory, admin, s.Id, "Eliminado");
        await AssignAsync(admin, revoked.Id, s.Gate);
        await AssignAsync(admin, deleted.Id, s.Hall);

        var revoke = await admin.PostAsync($"/api/v1/devices/{revoked.Id}/revocation", null);
        var newCode = await admin.PostAsync($"/api/v1/devices/{reissued.Id}/claim-code", null);
        var delete = await admin.DeleteAsync($"/api/v1/devices/{deleted.Id}");

        var revokedBody = await Json(revoke);
        Assert.Equal("revoked", revokedBody.GetProperty("status").GetString());
        Assert.Equal([s.Gate], revokedBody.GetProperty("accessPointIds").EnumerateArray().Select(e => e.GetGuid()));
        var issued = await Json(newCode);
        Assert.Equal("pending", issued.GetProperty("device").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        foreach (var device in new[] { revoked, reissued, deleted })
        {
            using var client = factory.CreateDeviceClient(device.Certificate);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/device/policy")).StatusCode);
        }

        var reclaimed = await ClaimAsync(factory, issued.GetProperty("claimCode").GetString()!);
        using var reclaimedClient = factory.CreateDeviceClient(reclaimed.Certificate);
        Assert.Equal(HttpStatusCode.OK, (await reclaimedClient.GetAsync("/api/v1/device/policy")).StatusCode);
        var newRevokeCode = await Json(await admin.PostAsync($"/api/v1/devices/{revoked.Id}/claim-code", null));
        await ClaimAsync(factory, newRevokeCode.GetProperty("claimCode").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/devices/{reissued.Id}/access-points", new { accessPointIds = new[] { s.Hall } })).StatusCode);
    }

    // --- Política ---

    [Fact]
    public async Task The_policy_package_is_signed_and_covers_the_device_points()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SiteWithPointsAsync(admin);
        var device = await CreateAndClaimAsync(factory, admin, s.Id);
        var idle = await CreateAndClaimAsync(factory, admin, s.Id, "Sin puntos");
        await AssignAsync(admin, device.Id, s.Hall, s.Gate);
        using var client = factory.CreateDeviceClient(device.Certificate);
        using var idleClient = factory.CreateDeviceClient(idle.Certificate);

        var (package, bytes, signature) = await PackageAsync(client);
        var (idlePackage, idleBytes, idleSignature) = await PackageAsync(idleClient);
        var tampered = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\"version\":1", "\"version\":2"));

        Assert.Equal(1, package.GetProperty("format").GetInt32());
        Assert.Equal((device.Id, s.Id, 1), (package.GetProperty("deviceId").GetGuid(), package.GetProperty("siteId").GetGuid(), package.GetProperty("version").GetInt32()));
        Assert.Equal(new[] { s.Gate, s.Hall }.Order(), package.GetProperty("points").EnumerateArray().Select(p => p.GetProperty("accessPointId").GetGuid()));
        Assert.All(package.GetProperty("points").EnumerateArray(), p => Assert.Equal(64, p.GetProperty("hash").GetString()!.Length));
        Assert.True(Verify(device.PolicyKey, bytes, signature));
        Assert.False(Verify(device.PolicyKey, tampered, signature));
        Assert.Empty(idlePackage.GetProperty("points").EnumerateArray());
        Assert.True(Verify(idle.PolicyKey, idleBytes, idleSignature));
    }

    [Fact]
    public async Task The_version_advances_only_when_the_content_changes()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SiteWithPointsAsync(admin);
        var (id, code) = await CreateDeviceAsync(admin, s.Id);
        var device = await ClaimAsync(factory, code);
        await AssignAsync(admin, id, s.Gate);
        using var client = factory.CreateDeviceClient(device.Certificate);

        var first = await PackageAsync(client);
        var again = await PackageAsync(client);
        var subject = await Json(await admin.PostAsJsonAsync($"/api/v1/sites/{s.Id}/subjects", new { kind = "person", alias = "Ana" }));
        await admin.PostAsJsonAsync($"/api/v1/sites/{s.Id}/credentials",
            new { subjectId = subject.GetProperty("id").GetGuid(), type = "card", value = "00A1B2C3" });
        var afterCredential = await PackageAsync(client);
        await admin.PostAsJsonAsync($"/api/v1/sites/{s.Id}/grants",
            new { subjectId = subject.GetProperty("id").GetGuid(), targetType = "access_point", targetId = s.Gate, effect = "allow" });
        var afterGrant = await PackageAsync(client);
        await AssignAsync(admin, id, s.Gate, s.Hall);
        var afterAssign = await PackageAsync(client);
        var notModified = await PolicyAsync(client, 4);
        var older = await PolicyAsync(client, 2);
        var reissued = await Json(await admin.PostAsync($"/api/v1/devices/{id}/claim-code", null));
        using var reclaimed = factory.CreateDeviceClient((await ClaimAsync(factory, reissued.GetProperty("claimCode").GetString()!)).Certificate);
        var afterReclaim = await PackageAsync(reclaimed);

        Assert.Equal(first.Bytes, again.Bytes);
        Assert.Equal([1, 2, 3, 4, 4], new[] { first, afterCredential, afterGrant, afterAssign, afterReclaim }.Select(p => p.Package.GetProperty("version").GetInt32()));
        Assert.Single(afterGrant.Package.GetProperty("points")[0].GetProperty("entries").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        Assert.Empty(await notModified.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.OK, older.StatusCode);
        Assert.Equal("\"4\"", older.Headers.ETag?.Tag);
        Assert.Equal(4, (await DeviceAsync(admin, id)).GetProperty("policyVersion").GetInt32());
    }

    // --- Latido ---

    [Fact]
    public async Task The_heartbeat_records_the_contact_and_returns_the_current_version()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SiteWithPointsAsync(admin);
        var device = await CreateAndClaimAsync(factory, admin, s.Id);
        await AssignAsync(admin, device.Id, s.Gate);
        using var client = factory.CreateDeviceClient(device.Certificate);
        await PackageAsync(client);
        await AssignAsync(admin, device.Id, s.Gate, s.Hall);

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var heartbeat = await client.PostAsJsonAsync("/api/v1/device/heartbeat", new { firmwareVersion = "1.0.3", policyVersion = 1 });
        var invalid = await client.PostAsJsonAsync("/api/v1/device/heartbeat", new { firmwareVersion = new string('v', 51), policyVersion = -1 });
        var body = await Json(heartbeat);
        var details = await DeviceAsync(admin, device.Id);

        Assert.Equal(HttpStatusCode.OK, heartbeat.StatusCode);
        Assert.Equal(2, body.GetProperty("policyVersion").GetInt32());
        Assert.InRange(body.GetProperty("serverTime").GetDateTimeOffset(), before, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(60, body.GetProperty("pollSeconds").GetInt32());
        Assert.Equal(("1.0.3", 1, 2), (details.GetProperty("firmwareVersion").GetString(), details.GetProperty("reportedPolicyVersion").GetInt32(),
            details.GetProperty("policyVersion").GetInt32()));
        Assert.True(details.GetProperty("lastSeenAt").GetDateTimeOffset() >= before);
        var errors = (await Json(invalid)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("FirmwareVersion", out _) && errors.TryGetProperty("PolicyVersion", out _));
    }

    // --- Privacidad y aislamiento ---

    [Fact]
    public async Task Claim_codes_keys_certificates_and_hashes_are_not_logged()
    {
        using var factory = new NexgateFactory();
        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var s = await SiteWithPointsAsync(admin);
        var (id, code) = await CreateDeviceAsync(admin, s.Id);
        var subject = await Json(await admin.PostAsJsonAsync($"/api/v1/sites/{s.Id}/subjects", new { kind = "person", alias = "Ana" }));
        await admin.PostAsJsonAsync($"/api/v1/sites/{s.Id}/credentials", new { subjectId = subject.GetProperty("id").GetGuid(), type = "card", value = "C0FFEE99" });
        await AssignAsync(admin, id, s.Gate);
        var device = await ClaimAsync(factory, code);
        using var client = factory.CreateDeviceClient(device.Certificate);
        var (package, _, _) = await PackageAsync(client);
        var hash = package.GetProperty("points")[0].GetProperty("credentials")[0].GetProperty("hash").GetString()!;
        var certificateBody = device.Claim.GetProperty("certificate").GetString()!.Split('\n')[1];

        var text = string.Join("\n", factory.Logs.Entries.Select(e => e.AllText));
        Assert.DoesNotContain(code, text);
        Assert.DoesNotContain(code.Replace("-", ""), text);
        Assert.DoesNotContain(device.CredentialKey, text);
        Assert.DoesNotContain(certificateBody, text);
        Assert.DoesNotContain(hash, text);
    }

    [Theory]
    [InlineData("devices.device")]
    [InlineData("devices.device_access_point")]
    [InlineData("devices.device_policy")]
    public async Task Device_tables_are_isolated_by_organization(string table)
    {
        var db = await TestDatabase.GetAsync();
        using var factory = new NexgateFactory(db);
        async Task<Guid> Tenant(string name)
        {
            var organization = await factory.CreateOrganizationAsync(name);
            using var client = factory.CreateClientFor(organization);
            var s = await SiteWithPointsAsync(client, $"Sitio {name}");
            var device = await CreateAndClaimAsync(factory, client, s.Id);
            await AssignAsync(client, device.Id, s.Gate);
            using var deviceClient = factory.CreateDeviceClient(device.Certificate);
            await PackageAsync(deviceClient);
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
