using System.Text.Json.Serialization;

namespace Nexgate.Service.Access.Contracts;

/// <summary>
/// Política compilada de un punto de acceso, formato v1 (design de add-access-evaluator, decisión 6). Es lo único que
/// necesita la decisión de acceso, en el backend y en el firmware: no requiere red ni base de zonas horarias. Los
/// instantes son segundos Unix; las ventanas, minutos del día locales con intervalo [from, to).
/// </summary>
public sealed record CompiledPolicy(
    int Format,
    Guid SiteId,
    Guid AccessPointId,
    string TimeZone,
    IReadOnlyList<PolicyOffset> Offsets,
    IReadOnlyList<IReadOnlyList<PolicyWindow>> Windows,
    IReadOnlyList<PolicyCredential> Credentials,
    IReadOnlyList<PolicyEntry> Entries,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Hash)
{
    public const int CurrentFormat = 1;
}

/// <summary>Desplazamiento UTC vigente desde <see cref="From"/> (nulo: desde antes de la compilación).</summary>
public sealed record PolicyOffset(long? From, int Seconds);

/// <summary>Una ventana horaria: <see cref="Days"/> es una máscara (bit 0 = lunes … bit 6 = domingo).</summary>
public sealed record PolicyWindow(int Days, int From, int To);

/// <summary>Una credencial activa: HMAC con la clave del sitio (base64url) y su vigencia [from, to).</summary>
public sealed record PolicyCredential(string Hash, long? From, long? To);

/// <summary>
/// Un permiso o una denegación de una credencial: <see cref="Window"/> es el índice de un horario en
/// <see cref="CompiledPolicy.Windows"/> (nulo: siempre); <see cref="Uses"/> nulo es sin límite.
/// </summary>
public sealed record PolicyEntry(int Credential, int? Window, long? From, long? To, int? Uses, bool Deny);

/// <summary>Resultado de la decisión de acceso (<see cref="AccessDecisionNames"/>, <see cref="AccessReasonNames"/>).</summary>
public sealed record AccessDecisionResult(string Decision, string Reason)
{
    public bool Allowed => Decision == AccessDecisionNames.Allow;
}
