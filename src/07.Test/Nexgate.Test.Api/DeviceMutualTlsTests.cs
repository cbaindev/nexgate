using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using static Nexgate.Test.Api.SpacesApiTests;

namespace Nexgate.Test.Api;

/// <summary>
/// Camino completo con Kestrel real y TLS mutuo (design de add-device-sync-protocol, decisión 3): el certificado viaja en
/// el handshake, no inyectado por la prueba.
/// </summary>
public class DeviceMutualTlsTests
{
    private static X509Certificate2 ServerCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256);
        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(IPAddress.Loopback);
        san.AddDnsName("localhost");
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        // SChannel no usa claves efímeras: se exporta y se vuelve a cargar.
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
    }

    [Fact]
    public async Task A_device_claims_and_downloads_its_policy_over_mutual_tls()
    {
        using var serverCertificate = ServerCertificate();
        // Fuera de Development: así no se cargan los endpoints de Kestrel de appsettings.Development.json.
        using var factory = new NexgateFactory { Environment = "Production", SigningKey = NexgateFactory.NewSigningKey() };
        factory.UseKestrel(kestrel =>
        {
            kestrel.Listen(IPAddress.Loopback, 0);
            kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(serverCertificate, https =>
            {
                https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
                https.AllowAnyClientCertificate();
            }));
        });
        factory.StartServer();
        var addresses = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
        factory.ClientOptions.BaseAddress = new Uri(addresses.Single(a => a.StartsWith("http://", StringComparison.Ordinal)));
        var deviceAddress = new Uri(addresses.Single(a => a.StartsWith("https://", StringComparison.Ordinal)));

        using var admin = factory.CreateClientFor(await factory.CreateOrganizationAsync());
        var site = await SiteAsync(admin);
        var root = await RootAsync(admin, site);
        var gate = await AccessPointApiTests.PointAsync(admin, site, "Portón", root, "gate");
        var (id, code) = await DeviceFlows.CreateDeviceAsync(admin, site);
        await DeviceFlows.AssignAsync(admin, id, gate);

        using var anonymousHandler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, c, _, _) => c?.Thumbprint == serverCertificate.Thumbprint };
        using var anonymous = new HttpClient(anonymousHandler) { BaseAddress = deviceAddress };
        var (key, csr) = DeviceKeys.NewCsr();
        var claim = await anonymous.PostAsJsonAsync("/api/v1/device/claim", new { claimCode = code, certificateRequest = csr });
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        var body = await claim.Content.ReadFromJsonAsync<JsonElement>();
        using var issued = X509Certificate2.CreateFromPem(body.GetProperty("certificate").GetString()!);
        using var withKey = issued.CopyWithPrivateKey(key);
        using var clientCertificate = X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pfx), null);

        using var deviceHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, c, _, _) => c?.Thumbprint == serverCertificate.Thumbprint,
            ClientCertificateOptions = ClientCertificateOption.Manual
        };
        deviceHandler.ClientCertificates.Add(clientCertificate);
        using var device = new HttpClient(deviceHandler) { BaseAddress = deviceAddress };
        var policy = await device.GetAsync("/api/v1/device/policy");
        var withoutCertificate = await anonymous.GetAsync("/api/v1/device/policy");

        Assert.Equal(HttpStatusCode.OK, policy.StatusCode);
        var bytes = await policy.Content.ReadAsByteArrayAsync();
        Assert.True(DevicesApiTests.Verify(body.GetProperty("policyKey").GetString()!, bytes, policy.Headers.GetValues("X-Nexgate-Signature").Single()));
        Assert.Equal(gate, JsonDocument.Parse(bytes).RootElement.GetProperty("points")[0].GetProperty("accessPointId").GetGuid());
        Assert.Equal(HttpStatusCode.Unauthorized, withoutCertificate.StatusCode);
    }
}
