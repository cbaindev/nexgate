using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Nexgate.BuildingBlocks.Access;
using Nexgate.Modules.Identity.Application;
using Nexgate.Modules.Identity.Domain;

namespace Nexgate.Modules.Identity.Infrastructure;

/// <summary>Configuración del módulo (sección <c>Identity</c>).</summary>
public sealed class IdentityModuleOptions
{
    public const string SectionName = "Identity";

    public TokenSettings Tokens { get; set; } = new();

    public PasswordSettings Passwords { get; set; } = new();

    public RateLimitSettings RateLimit { get; set; } = new();

    public sealed class TokenSettings
    {
        /// <summary>
        /// Clave privada EC P-256 en PEM para firmar los tokens de acceso. Obligatoria fuera de Development; en
        /// Development, si falta, se genera una efímera al arrancar.
        /// </summary>
        public string? SigningKey { get; set; }

        public int AccessTokenMinutes { get; set; } = 15;

        public int RefreshSessionDays { get; set; } = 30;

        public int InvitationDays { get; set; } = 7;
    }

    public sealed class PasswordSettings
    {
        /// <summary>Iteraciones de PBKDF2. Vacío usa el valor por defecto de la plataforma; las pruebas lo bajan.</summary>
        public int? IterationCount { get; set; }
    }

    public sealed class RateLimitSettings
    {
        /// <summary>Intentos por minuto y por IP en inicio de sesión y aceptación de invitaciones.</summary>
        public int PermitsPerMinute { get; set; } = 10;
    }
}

/// <summary>No deja arrancar fuera de Development sin una clave de firma válida (sin mostrar la clave).</summary>
public sealed class IdentityModuleOptionsValidator(IHostEnvironment environment) : IValidateOptions<IdentityModuleOptions>
{
    public ValidateOptionsResult Validate(string? name, IdentityModuleOptions options)
    {
        if (options.Tokens.AccessTokenMinutes <= 0 || options.Tokens.RefreshSessionDays <= 0 || options.Tokens.InvitationDays <= 0
            || options.RateLimit.PermitsPerMinute <= 0)
        {
            return ValidateOptionsResult.Fail("Las duraciones y el límite de intentos de Identity deben ser positivos.");
        }

        var key = options.Tokens.SigningKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return environment.IsDevelopment()
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail("Identity:Tokens:SigningKey es obligatoria fuera del entorno Development.");
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(key);
            if (ecdsa.KeySize != 256)
            {
                return ValidateOptionsResult.Fail("Identity:Tokens:SigningKey debe ser una clave EC P-256.");
            }
        }
        catch (Exception e) when (e is ArgumentException or CryptographicException)
        {
            return ValidateOptionsResult.Fail("Identity:Tokens:SigningKey no es una clave EC P-256 válida en formato PEM.");
        }

        return ValidateOptionsResult.Success;
    }
}

/// <summary>Clave de firma de los tokens de acceso (ES256). El <c>kid</c> es la huella de la clave.</summary>
public sealed class TokenSigningKey : IDisposable
{
    public const string Issuer = "nexgate";
    public const string Audience = "nexgate-api";

    private readonly ECDsa _ecdsa;

    public TokenSigningKey(IOptions<IdentityModuleOptions> options, ILogger<TokenSigningKey> logger)
    {
        var pem = options.Value.Tokens.SigningKey;
        if (string.IsNullOrWhiteSpace(pem))
        {
            // Solo llega acá en Development (lo garantiza el validador de opciones).
            _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            logger.LogWarning("Sin Identity:Tokens:SigningKey: se usa una clave efímera; los tokens dejan de valer al reiniciar.");
        }
        else
        {
            _ecdsa = ECDsa.Create();
            _ecdsa.ImportFromPem(pem);
        }

        var key = new ECDsaSecurityKey(_ecdsa);
        key.KeyId = Base64UrlEncoder.Encode(JsonWebKeyConverter.ConvertFromECDsaSecurityKey(key).ComputeJwkThumbprint());
        Key = key;
    }

    public ECDsaSecurityKey Key { get; }

    public void Dispose() => _ecdsa.Dispose();
}

internal sealed class JwtAccessTokenIssuer(TokenSigningKey signingKey, IOptions<IdentityModuleOptions> options, TimeProvider clock)
    : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(Guid userId, Guid? organizationId, Guid sessionId)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(options.Value.Tokens.AccessTokenMinutes);
        var claims = new Dictionary<string, object>
        {
            [AccessClaims.Subject] = userId.ToString(),
            [AccessClaims.Session] = sessionId.ToString()
        };
        if (organizationId is { } organization)
        {
            claims[AccessClaims.Organization] = organization.ToString();
        }

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = TokenSigningKey.Issuer,
            Audience = TokenSigningKey.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(signingKey.Key, SecurityAlgorithms.EcdsaSha256)
        });
        return new AccessToken(token, expires);
    }
}

/// <summary>PBKDF2 de ASP.NET Core Identity (formato versionado, con sal por usuario).</summary>
internal sealed class PasswordHashing : IPasswordHashing
{
    private readonly PasswordHasher<UserAccount> _hasher;
    private readonly Lazy<string> _dummy;

    public PasswordHashing(IOptions<PasswordHasherOptions> options)
    {
        _hasher = new PasswordHasher<UserAccount>(options);
        _dummy = new Lazy<string>(() => _hasher.HashPassword(null!, Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))));
    }

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public PasswordCheck Verify(string passwordHash, string password) => _hasher.VerifyHashedPassword(null!, passwordHash, password) switch
    {
        PasswordVerificationResult.Success => PasswordCheck.Success,
        PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
        _ => PasswordCheck.Failed
    };

    public void VerifyAgainstDummy(string password) => _hasher.VerifyHashedPassword(null!, _dummy.Value, password);
}
