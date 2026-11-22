using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Nexgate.Service.Access;

/// <summary>Configuración del módulo Access (sección <c>Access</c>).</summary>
public sealed class AccessOptions
{
    public const string SectionName = "Access";

    /// <summary>
    /// Clave maestra (base64, al menos 32 bytes) de la que se deriva la clave HMAC de credenciales de cada sitio. En
    /// Development, si falta, se usa <see cref="DevelopmentCredentialKey"/>; fuera de Development es obligatoria y no puede
    /// ser esa.
    /// </summary>
    public string? CredentialKey { get; set; }

    /// <summary>Clave fija y pública solo para desarrollo: las credenciales cargadas sobreviven a los reinicios.</summary>
    public const string DevelopmentCredentialKey = "bmV4Z2F0ZS1kZXNhcnJvbGxvLWNsYXZlLWRlLWNyZWRlbmNpYWxlcy0wMQ==";

    public const int MinimumKeyBytes = 32;
}

public sealed class AccessOptionsValidator(IHostEnvironment environment) : IValidateOptions<AccessOptions>
{
    public ValidateOptionsResult Validate(string? name, AccessOptions options)
    {
        var key = options.CredentialKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return environment.IsDevelopment()
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail("Access:CredentialKey es obligatoria fuera del entorno Development.");
        }

        if (!environment.IsDevelopment() && key == AccessOptions.DevelopmentCredentialKey)
        {
            return ValidateOptionsResult.Fail("Access:CredentialKey no puede ser la clave de desarrollo fuera del entorno Development.");
        }

        try
        {
            return Convert.FromBase64String(key).Length >= AccessOptions.MinimumKeyBytes
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail($"Access:CredentialKey debe tener al menos {AccessOptions.MinimumKeyBytes} bytes.");
        }
        catch (FormatException)
        {
            return ValidateOptionsResult.Fail("Access:CredentialKey debe estar en base64.");
        }
    }
}

/// <summary>La clave maestra de credenciales, ya decodificada.</summary>
public sealed class CredentialKey(IOptions<AccessOptions> options)
{
    public byte[] Master { get; } = Convert.FromBase64String(
        string.IsNullOrWhiteSpace(options.Value.CredentialKey) ? AccessOptions.DevelopmentCredentialKey : options.Value.CredentialKey);
}
