using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Nexgate.Service.Devices.Contracts;

namespace Nexgate.Domain.Devices;

/// <summary>
/// Código de reclamo (design, decisión 2): 10 caracteres de un alfabeto sin ambiguos (unos 49 bits), mostrado como
/// <c>ABCDE-23456</c> y aceptado sin guiones, espacios ni mayúsculas. Se guarda solo su SHA-256.
/// </summary>
public static class ClaimCode
{
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    public const int Length = 10;

    public static string Generate()
    {
        var chars = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars, 0, 5) + "-" + new string(chars, 5, 5);
    }

    /// <summary>El código sin separadores y en mayúsculas; null si no tiene la forma de un código.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 40)
        {
            return null;
        }

        var normalized = new string(input.Where(c => c != '-' && !char.IsWhiteSpace(c)).Select(char.ToUpperInvariant).ToArray());
        return normalized.Length == Length && normalized.All(Alphabet.Contains) ? normalized : null;
    }

    public static byte[] Hash(string normalized) => SHA256.HashData(Encoding.ASCII.GetBytes(normalized));
}

/// <summary>
/// Emisión de certificados de cliente de dispositivo desde un CSR PKCS#10 con clave P-256 (design, decisión 2). El sujeto
/// del CSR se ignora: el certificado identifica al dispositivo con <c>CN=&lt;deviceId&gt;</c>.
/// </summary>
public static class DeviceCertificates
{
    public const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";
    private const string Field = "CertificateRequest";

    public static X509Certificate2 Issue(string? certificateRequestPem, Guid deviceId, X509Certificate2 authority,
        DateTimeOffset now, TimeSpan validity)
    {
        if (string.IsNullOrWhiteSpace(certificateRequestPem) || certificateRequestPem.Length > DeviceLimits.CertificateRequestMaxLength)
        {
            throw new DomainValidationException(Field, "La solicitud de certificado es obligatoria, en PEM.");
        }

        CertificateRequest csr;
        try
        {
            // Por defecto se verifica la firma del CSR: prueba que el dispositivo tiene la clave privada.
            csr = CertificateRequest.LoadSigningRequestPem(certificateRequestPem, HashAlgorithmName.SHA256);
        }
        catch (Exception e) when (e is CryptographicException or ArgumentException)
        {
            throw new DomainValidationException(Field, "La solicitud de certificado no se puede leer o su firma es inválida.");
        }

        using (var key = csr.PublicKey.GetECDsaPublicKey())
        {
            if (key is null || key.ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
            {
                throw new DomainValidationException(Field, "La clave de la solicitud debe ser de curva elíptica P-256.");
            }
        }

        var request = new CertificateRequest(new X500DistinguishedName("CN=" + deviceId.ToString("D")), csr.PublicKey, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(ClientAuthOid)], false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(authority, true, false));

        var notBefore = now.AddMinutes(-5);
        var notAfter = now + validity;
        if (notAfter > authority.NotAfter)
        {
            notAfter = authority.NotAfter;
        }

        return request.Create(authority, notBefore, notAfter, RandomNumberGenerator.GetBytes(16));
    }

    /// <summary>Huella del certificado: SHA-256 del DER en hex minúsculas.</summary>
    public static string Thumbprint(X509Certificate2 certificate) => Thumbprint(certificate.RawData);

    public static string Thumbprint(byte[] certificateDer) => Convert.ToHexStringLower(SHA256.HashData(certificateDer));
}

/// <summary>
/// Firma de los paquetes de política (design, decisión 4): ECDSA P-256 con SHA-256 sobre los bytes exactos del paquete,
/// en formato IEEE P1363 (r || s, 64 bytes) y base64url sin relleno.
/// </summary>
public static class PolicySignature
{
    public static string Sign(ECDsa key, byte[] content) =>
        Base64Url(key.SignData(content, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

    public static bool Verify(ECDsa publicKey, byte[] content, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        try
        {
            return publicKey.VerifyData(content, FromBase64Url(signature), HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
