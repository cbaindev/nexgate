using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Nexgate.Service.Devices;

/// <summary>Configuración del módulo Devices (sección <c>Devices</c>).</summary>
public sealed class DevicesOptions
{
    public const string SectionName = "Devices";

    /// <summary>Autoridad de certificación de dispositivos (PEM). En Development, si falta, se usa la de desarrollo.</summary>
    public AuthorityOptions CertificateAuthority { get; set; } = new();

    /// <summary>Clave privada ECDSA P-256 (PEM) que firma los paquetes de política.</summary>
    public string? PolicySigningKey { get; set; }

    /// <summary>Cada cuántos segundos late el dispositivo.</summary>
    public int PollSeconds { get; set; } = 60;

    public int CertificateValidityDays { get; set; } = 1095;

    public int ClaimCodeDays { get; set; } = 7;

    /// <summary>Reclamos por minuto y por IP.</summary>
    public int ClaimPermitsPerMinute { get; set; } = 10;

    public sealed class AuthorityOptions
    {
        public string? Certificate { get; set; }

        public string? Key { get; set; }
    }

    /// <summary>Autoridad fija y pública solo para desarrollo: los dispositivos reclamados sobreviven a los reinicios.</summary>
    public const string DevelopmentAuthorityCertificate = """
        -----BEGIN CERTIFICATE-----
        MIIBvTCCAWOgAwIBAgIUF4/fdoFAEBPp0D5/pE0jczq+DKQwCgYIKoZIzj0EAwIw
        OzEnMCUGA1UEAwweTmV4Z2F0ZSBEZXZpY2UgQ0EgKERFU0FSUk9MTE8pMRAwDgYD
        VQQKDAdOZXhnYXRlMB4XDTI2MTEyMjIyNDg1NloXDTQ2MTExNzIyNDg1NlowOzEn
        MCUGA1UEAwweTmV4Z2F0ZSBEZXZpY2UgQ0EgKERFU0FSUk9MTE8pMRAwDgYDVQQK
        DAdOZXhnYXRlMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEs5D31SnQaqESV3ku
        hm+anGzOS00mVF1eYPbFEzXKkoZF+ud4d1PS5cXw6CBjZNVAHKGNlNP4+rpuEp2s
        K4nTC6NFMEMwEgYDVR0TAQH/BAgwBgEB/wIBADAOBgNVHQ8BAf8EBAMCAQYwHQYD
        VR0OBBYEFJqfeDeLLJtOVmMHF9RqrmTSIwd7MAoGCCqGSM49BAMCA0gAMEUCIQCo
        T8GseyNxb96CiasP22l2KSY5XthKGAP7b2rNPkLKhAIgARt3UJ2h6CbK5GbH906O
        h3voOOSmjHGASxaeQTjh+NE=
        -----END CERTIFICATE-----
        """;

    public const string DevelopmentAuthorityKey = """
        -----BEGIN PRIVATE KEY-----
        MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgfiACn4cTU+WN/Mht
        ohrBk8tbwA2fDexklwrqzGxDfBihRANCAASzkPfVKdBqoRJXeS6Gb5qcbM5LTSZU
        XV5g9sUTNcqShkX653h3U9LlxfDoIGNk1UAcoY2U0/j6um4SnawridML
        -----END PRIVATE KEY-----
        """;

    /// <summary>Clave de firma de políticas fija y pública solo para desarrollo.</summary>
    public const string DevelopmentPolicySigningKey = """
        -----BEGIN PRIVATE KEY-----
        MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgwZ8/uNwPnMWtuyP7
        PY7QxXro60JkgHJH9haDWqfRX6ShRANCAASDMQ2iggTSJJE3SFjILzZFA8/wTnWW
        +ui1e/+6Av/gNNM1/je6Wk/5SJR75+s+HKBkwLn7K2vklSeVo3hzTZ56
        -----END PRIVATE KEY-----
        """;

    internal string AuthorityCertificatePem => string.IsNullOrWhiteSpace(CertificateAuthority.Certificate)
        ? DevelopmentAuthorityCertificate
        : CertificateAuthority.Certificate;

    internal string AuthorityKeyPem => string.IsNullOrWhiteSpace(CertificateAuthority.Key)
        ? DevelopmentAuthorityKey
        : CertificateAuthority.Key;

    internal string PolicySigningKeyPem => string.IsNullOrWhiteSpace(PolicySigningKey) ? DevelopmentPolicySigningKey : PolicySigningKey;
}

public sealed class DevicesOptionsValidator(IHostEnvironment environment) : IValidateOptions<DevicesOptions>
{
    public ValidateOptionsResult Validate(string? name, DevicesOptions options)
    {
        var errors = new List<string>();
        var production = !environment.IsDevelopment();
        var authority = options.CertificateAuthority;
        if (production)
        {
            if (string.IsNullOrWhiteSpace(authority.Certificate) || string.IsNullOrWhiteSpace(authority.Key))
            {
                errors.Add("Devices:CertificateAuthority (Certificate y Key) es obligatoria fuera del entorno Development.");
            }
            else if (Same(authority.Key, DevicesOptions.DevelopmentAuthorityKey))
            {
                errors.Add("Devices:CertificateAuthority no puede ser la de desarrollo fuera del entorno Development.");
            }

            if (string.IsNullOrWhiteSpace(options.PolicySigningKey))
            {
                errors.Add("Devices:PolicySigningKey es obligatoria fuera del entorno Development.");
            }
            else if (Same(options.PolicySigningKey, DevicesOptions.DevelopmentPolicySigningKey))
            {
                errors.Add("Devices:PolicySigningKey no puede ser la de desarrollo fuera del entorno Development.");
            }
        }

        if (errors.Count == 0)
        {
            try
            {
                using var certificate = X509Certificate2.CreateFromPem(options.AuthorityCertificatePem, options.AuthorityKeyPem);
                using var key = certificate.GetECDsaPrivateKey();
                if (key is null || !certificate.HasPrivateKey)
                {
                    errors.Add("Devices:CertificateAuthority debe ser un certificado ECDSA con su clave privada.");
                }
            }
            catch (CryptographicException)
            {
                errors.Add("Devices:CertificateAuthority no se puede leer (certificado y clave en PEM, que coincidan).");
            }

            try
            {
                using var key = ECDsa.Create();
                key.ImportFromPem(options.PolicySigningKeyPem);
                if (key.KeySize != 256)
                {
                    errors.Add("Devices:PolicySigningKey debe ser una clave P-256.");
                }
            }
            catch (Exception e) when (e is CryptographicException or ArgumentException)
            {
                errors.Add("Devices:PolicySigningKey no se puede leer (clave ECDSA P-256 en PEM).");
            }
        }

        if (options.PollSeconds is < 10 or > 3600)
        {
            errors.Add("Devices:PollSeconds debe estar entre 10 y 3600.");
        }

        if (options.CertificateValidityDays < 1 || options.ClaimCodeDays < 1 || options.ClaimPermitsPerMinute < 1)
        {
            errors.Add("Devices: la vigencia del certificado, la del código y el límite de reclamos deben ser positivos.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool Same(string a, string b) => Normalize(a) == Normalize(b);

    private static string Normalize(string pem) => string.Concat(pem.Where(c => !char.IsWhiteSpace(c)));
}

/// <summary>Autoridad de certificación de dispositivos: firma los certificados y es la única raíz que se acepta.</summary>
public sealed class DeviceCertificateAuthority : IDisposable
{
    public DeviceCertificateAuthority(IOptions<DevicesOptions> options)
    {
        Certificate = X509Certificate2.CreateFromPem(options.Value.AuthorityCertificatePem, options.Value.AuthorityKeyPem);
        PublicCertificate = X509Certificate2.CreateFromPem(options.Value.AuthorityCertificatePem);
        CertificatePem = PublicCertificate.ExportCertificatePem();
    }

    /// <summary>Con clave privada: solo para emitir.</summary>
    internal X509Certificate2 Certificate { get; }

    /// <summary>Sin clave privada: raíz de confianza de la autenticación por certificado.</summary>
    public X509Certificate2 PublicCertificate { get; }

    public string CertificatePem { get; }

    public void Dispose()
    {
        Certificate.Dispose();
        PublicCertificate.Dispose();
    }
}

/// <summary>Clave que firma los paquetes de política; el dispositivo recibe la pública al reclamarlo.</summary>
public sealed class PolicySigningKey : IDisposable
{
    public PolicySigningKey(IOptions<DevicesOptions> options)
    {
        Key = ECDsa.Create();
        Key.ImportFromPem(options.Value.PolicySigningKeyPem);
        PublicKeyPem = Key.ExportSubjectPublicKeyInfoPem();
    }

    internal ECDsa Key { get; }

    public string PublicKeyPem { get; }

    public void Dispose() => Key.Dispose();
}
