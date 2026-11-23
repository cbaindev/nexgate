using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Nexgate.Test.Api;

/// <summary>
/// Pone el certificado de cliente en la conexión, donde lo deja Kestrel tras el TLS mutuo: <c>TestServer</c> no hace TLS.
/// Así el esquema real de autenticación de dispositivos corre entero (design de add-device-sync-protocol, decisión 3).
/// </summary>
public sealed class TestClientCertificate : IStartupFilter
{
    public const string Header = "X-Test-Client-Certificate";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(Header, out var value))
            {
                context.Connection.ClientCertificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(value.ToString()));
                context.Request.Headers.Remove(Header);
                // Con TLS mutuo la solicitud es HTTPS; el esquema de certificado ignora las que no lo son.
                context.Request.Scheme = "https";
            }

            await nextMiddleware(context);
        });
        next(app);
    };
}

/// <summary>Claves y certificados de dispositivos de las pruebas, propios de la corrida (no los de desarrollo).</summary>
public static class DeviceKeys
{
    public sealed record Authority(string CertificatePem, string KeyPem, X509Certificate2 Certificate);

    public static readonly Authority TestAuthority = NewAuthority("Nexgate Device CA (PRUEBAS)");

    public static readonly string TestPolicySigningKey = NewSigningKey();

    public static string NewSigningKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return key.ExportPkcs8PrivateKeyPem();
    }

    public static Authority NewAuthority(string name)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=" + name, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        return new Authority(certificate.ExportCertificatePem(), key.ExportPkcs8PrivateKeyPem(), certificate);
    }

    /// <summary>Clave P-256 nueva y su solicitud de certificado en PEM, como las genera el dispositivo.</summary>
    public static (ECDsa Key, string Csr) NewCsr(string subject = "CN=dispositivo")
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (key, new CertificateRequest(subject, key, HashAlgorithmName.SHA256).CreateSigningRequestPem());
    }

    /// <summary>Certificado de cliente con el sujeto indicado, firmado por otra autoridad.</summary>
    public static X509Certificate2 ForeignClientCertificate(Authority authority, string subject)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.2")], false));
        using var issuer = X509Certificate2.CreateFromPem(authority.CertificatePem, authority.KeyPem);
        return request.Create(issuer, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1), RandomNumberGenerator.GetBytes(16));
    }
}

/// <summary>Dispositivo reclamado en una prueba: su certificado (con clave privada) y lo que devolvió el reclamo.</summary>
public sealed record ClaimedDevice(Guid Id, Guid SiteId, X509Certificate2 Certificate, JsonElement Claim)
{
    public string PolicyKey => Claim.GetProperty("policyKey").GetString()!;

    public string CredentialKey => Claim.GetProperty("credentialKey").GetString()!;
}

internal static class DeviceFlows
{
    public static async Task<(Guid Id, string Code)> CreateDeviceAsync(HttpClient admin, Guid site, string name = "Controlador portón")
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/sites/{site}/devices", new { name });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("device").GetProperty("id").GetGuid(), body.GetProperty("claimCode").GetString()!);
    }

    public static async Task<HttpResponseMessage> PostClaimAsync(NexgateFactory factory, string code, string csr)
    {
        using var client = factory.CreateClient();
        return await client.PostAsJsonAsync("/api/v1/device/claim", new { claimCode = code, certificateRequest = csr });
    }

    public static async Task<ClaimedDevice> ClaimAsync(NexgateFactory factory, string code)
    {
        var (key, csr) = DeviceKeys.NewCsr();
        var response = await PostClaimAsync(factory, code, csr);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        using var certificate = X509Certificate2.CreateFromPem(body.GetProperty("certificate").GetString()!);
        var withKey = certificate.CopyWithPrivateKey(key);
        return new ClaimedDevice(body.GetProperty("deviceId").GetGuid(), body.GetProperty("siteId").GetGuid(), withKey, body);
    }

    public static async Task<ClaimedDevice> CreateAndClaimAsync(NexgateFactory factory, HttpClient admin, Guid site, string name = "Controlador portón")
    {
        var (_, code) = await CreateDeviceAsync(admin, site, name);
        return await ClaimAsync(factory, code);
    }

    public static async Task AssignAsync(HttpClient admin, Guid device, params Guid[] points)
    {
        var response = await admin.PutAsJsonAsync($"/api/v1/devices/{device}/access-points", new { accessPointIds = points });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
