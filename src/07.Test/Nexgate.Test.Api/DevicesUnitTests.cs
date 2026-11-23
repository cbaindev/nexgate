using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Nexgate.Domain.Devices;
using Nexgate.Service.Access.Contracts;
using Nexgate.Service.Devices;

namespace Nexgate.Test.Api;

/// <summary>Reglas puras de Devices: código de reclamo, emisión de certificados, firma y huella del paquete.</summary>
public class DevicesUnitTests
{
    [Fact]
    public void Claim_codes_use_an_unambiguous_alphabet_and_are_normalized()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => ClaimCode.Generate()).ToList();

        Assert.All(codes, c => Assert.Matches("^[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{5}-[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{5}$", c));
        Assert.True(codes.Distinct().Count() > 190);
        Assert.Equal("ABCDE23456", ClaimCode.Normalize(" abcde 23456 "));
        Assert.Equal("ABCDE23456", ClaimCode.Normalize("ABCDE-23456"));
        Assert.Null(ClaimCode.Normalize("ABCDE-2345"));
        Assert.Null(ClaimCode.Normalize("ABCDE-0O1IL"));
        Assert.Null(ClaimCode.Normalize(null));
        Assert.Equal(ClaimCode.Hash("ABCDE23456"), ClaimCode.Hash(ClaimCode.Normalize("abcde-23456")!));
    }

    [Fact]
    public void A_certificate_is_issued_for_the_device_from_a_valid_request()
    {
        var authority = DeviceKeys.NewAuthority("Autoridad de prueba");
        using var issuer = X509Certificate2.CreateFromPem(authority.CertificatePem, authority.KeyPem);
        var (key, csr) = DeviceKeys.NewCsr("CN=lo que diga el dispositivo");
        var deviceId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var certificate = DeviceCertificates.Issue(csr, deviceId, issuer, now, TimeSpan.FromDays(1095));
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(authority.Certificate);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        Assert.Equal("CN=" + deviceId.ToString("D"), certificate.Subject);
        Assert.Equal(authority.Certificate.Subject, certificate.Issuer);
        Assert.True(chain.Build(certificate));
        Assert.Contains(certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>(),
            e => e.EnhancedKeyUsages.Cast<Oid>().Any(o => o.Value == DeviceCertificates.ClientAuthOid));
        Assert.Equal(key.ExportSubjectPublicKeyInfo(), certificate.GetECDsaPublicKey()!.ExportSubjectPublicKeyInfo());
        Assert.InRange(certificate.NotAfter.ToUniversalTime(), now.AddDays(1094).UtcDateTime, now.AddDays(1096).UtcDateTime);
        Assert.Equal(64, DeviceCertificates.Thumbprint(certificate).Length);
    }

    [Fact]
    public void Invalid_requests_are_rejected()
    {
        var authority = DeviceKeys.NewAuthority("Autoridad de prueba");
        using var issuer = X509Certificate2.CreateFromPem(authority.CertificatePem, authority.KeyPem);
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var otherCurve = new CertificateRequest("CN=x", p384, HashAlgorithmName.SHA256).CreateSigningRequestPem();
        var (_, valid) = DeviceKeys.NewCsr();
        var der = Convert.FromBase64String(string.Concat(valid.Split('\n').Where(l => !l.StartsWith("-----"))));
        der[^5] ^= 0xFF;
        var badSignature = PemEncoding.WriteString("CERTIFICATE REQUEST", der);

        foreach (var request in new[] { null, "", "texto cualquiera", otherCurve, badSignature })
        {
            var error = Assert.Throws<DomainValidationException>(() =>
                DeviceCertificates.Issue(request, Guid.NewGuid(), issuer, DateTimeOffset.UtcNow, TimeSpan.FromDays(1)));
            Assert.Equal("CertificateRequest", error.Field);
        }
    }

    [Fact]
    public void Signatures_verify_only_the_exact_content_with_the_right_key()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var content = "{\"format\":1,\"version\":3}"u8.ToArray();
        var tampered = "{\"format\":1,\"version\":4}"u8.ToArray();

        var signature = PolicySignature.Sign(key, content);

        Assert.Equal(86, signature.Length);
        Assert.True(PolicySignature.Verify(key, content, signature));
        Assert.False(PolicySignature.Verify(key, tampered, signature));
        Assert.False(PolicySignature.Verify(other, content, signature));
        Assert.False(PolicySignature.Verify(key, content, "no-es-base64url!"));
    }

    [Fact]
    public void The_content_hash_depends_only_on_the_points()
    {
        var site = Guid.NewGuid();
        var point = Guid.NewGuid();
        CompiledPolicy Policy(int uses) => new(1, site, point, "America/Argentina/Cordoba", [new PolicyOffset(null, -10800)], [],
            [new PolicyCredential("hash", null, null)], [new PolicyEntry(0, null, null, null, uses, false)], "h");

        Assert.Equal(DevicePolicyService.ContentHash([Policy(2)]), DevicePolicyService.ContentHash([Policy(2)]));
        Assert.NotEqual(DevicePolicyService.ContentHash([Policy(2)]), DevicePolicyService.ContentHash([Policy(1)]));
        Assert.NotEqual(DevicePolicyService.ContentHash([Policy(2)]), DevicePolicyService.ContentHash([]));
    }

    [Theory]
    [InlineData("2026-11-23T11:00:00Z", 0, 480)] // lunes 8:00 en Córdoba
    [InlineData("2026-11-29T23:30:00Z", 6, 1230)] // domingo 20:30
    [InlineData("2026-11-30T02:59:00Z", 6, 1439)] // domingo 23:59 local, lunes en UTC
    public void Local_time_for_use_consumption_follows_the_site_zone(string instant, int day, int minute)
    {
        Assert.Equal((day, minute), Nexgate.Service.Access.GrantUses.LocalTime("America/Argentina/Cordoba", DateTimeOffset.Parse(instant)));
    }
}
