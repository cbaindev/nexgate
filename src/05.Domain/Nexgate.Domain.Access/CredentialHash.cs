using System.Security.Cryptography;
using System.Text;

namespace Nexgate.Domain.Access;

/// <summary>
/// HMAC de credenciales con una clave por sitio (design de add-access-evaluator, decisión 2). La clave del sitio se
/// deriva de la clave maestra; el dispositivo del sitio recibe la clave del sitio y calcula el mismo HMAC sobre lo que lee.
/// </summary>
public static class CredentialHash
{
    private const string SiteKeyLabel = "nexgate/credential-site/v1/";

    /// <summary>Normaliza el valor: sin espacios alrededor; rf y tarjeta en mayúsculas; el PIN tal cual.</summary>
    public static string Normalize(CredentialType type, string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > Service.Access.Contracts.AccessLimits.CredentialValueMaxLength)
        {
            throw new DomainValidationException("Value", "El valor de la credencial tiene entre 1 y 64 caracteres.");
        }

        return type == CredentialType.Pin ? trimmed : trimmed.ToUpperInvariant();
    }

    public static byte[] SiteKey(byte[] masterKey, Guid siteId) =>
        HMACSHA256.HashData(masterKey, Encoding.UTF8.GetBytes(SiteKeyLabel + siteId.ToString("D")));

    /// <summary>HMAC-SHA256(claveDelSitio, tipo + ":" + valorNormalizado).</summary>
    public static byte[] Compute(byte[] masterKey, Guid siteId, CredentialType type, string? value) =>
        HMACSHA256.HashData(SiteKey(masterKey, siteId), Encoding.UTF8.GetBytes(AccessNames.ToName(type) + ":" + Normalize(type, value)));

    /// <summary>Representación del hash en la política compilada (base64url sin relleno).</summary>
    public static string ToPolicy(byte[] hash) =>
        Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
