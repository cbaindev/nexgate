using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nexgate.Service.Access.Contracts;

namespace Nexgate.Domain.Access;

/// <summary>Una credencial a compilar: su hash para la política y su vigencia.</summary>
public sealed record CompilableCredential(Guid SubjectId, string Hash, DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil);

/// <summary>Un otorgamiento que cubre el punto de acceso, con las ventanas de su horario (nulo: siempre).</summary>
public sealed record CompilableGrant(
    Guid SubjectId,
    IReadOnlyList<ScheduleWindow>? Windows,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    bool Deny,
    int? RemainingUses);

/// <summary>
/// Compila la política v1 de un punto de acceso (design de add-access-evaluator, decisión 6) a partir de los
/// otorgamientos que ya se sabe que lo cubren. Es determinista: el mismo contenido da el mismo hash.
/// </summary>
public static class PolicyCompiler
{
    private static readonly JsonSerializerOptions Canonical = new(JsonSerializerDefaults.Web);

    public static CompiledPolicy Compile(
        Guid siteId,
        Guid accessPointId,
        string timeZone,
        IReadOnlyList<PolicyOffset> offsets,
        IReadOnlyList<CompilableCredential> credentials,
        IReadOnlyList<CompilableGrant> grants,
        DateTimeOffset now)
    {
        var activeCredentials = credentials.Where(c => c.ValidUntil is not { } until || until > now).ToList();
        var raw = new List<(CompilableCredential Credential, IReadOnlyList<PolicyWindow>? Window, long? From, long? To, int? Uses, bool Deny)>();
        foreach (var grant in grants.Where(g => g.ValidUntil is not { } until || until > now))
        {
            var window = grant.Windows?.Select(w => new PolicyWindow(w.Days, w.From, w.To))
                .OrderBy(w => w.From).ThenBy(w => w.To).ThenBy(w => w.Days).ToList();
            foreach (var credential in activeCredentials.Where(c => c.SubjectId == grant.SubjectId))
            {
                var from = Max(credential.ValidFrom, grant.ValidFrom);
                var to = Min(credential.ValidUntil, grant.ValidUntil);
                if (from is { } f && to is { } t && t <= f)
                {
                    continue;
                }

                raw.Add((credential, window, from?.ToUnixTimeSeconds(), to?.ToUnixTimeSeconds(), grant.Deny ? null : grant.RemainingUses, grant.Deny));
            }
        }

        // Todas las credenciales activas del sitio, ordenadas por hash (así una credencial válida sin permiso en este
        // punto da no_permission y no credential_invalid); los horarios, por su forma canónica.
        var policyCredentials = activeCredentials
            .OrderBy(c => c.Hash, StringComparer.Ordinal)
            .Select(c => new PolicyCredential(c.Hash, c.ValidFrom?.ToUnixTimeSeconds(), c.ValidUntil?.ToUnixTimeSeconds()))
            .Distinct()
            .ToList();
        var windows = raw.Where(r => r.Window is not null).Select(r => r.Window!)
            .DistinctBy(Key).OrderBy(Key, StringComparer.Ordinal).ToList();

        var entries = raw
            .Select(r => new PolicyEntry(
                policyCredentials.FindIndex(c => c.Hash == r.Credential.Hash),
                r.Window is null ? null : windows.FindIndex(w => Key(w) == Key(r.Window)),
                r.From, r.To, r.Uses, r.Deny))
            .Distinct()
            .OrderBy(e => e.Credential).ThenBy(e => e.Deny).ThenBy(e => e.Window ?? -1)
            .ThenBy(e => e.From ?? long.MinValue).ThenBy(e => e.To ?? long.MaxValue).ThenBy(e => e.Uses ?? int.MaxValue)
            .ToList();

        var policy = new CompiledPolicy(CompiledPolicy.CurrentFormat, siteId, accessPointId, timeZone, offsets,
            windows, policyCredentials, entries, null);
        return policy with { Hash = HashOf(policy) };
    }

    /// <summary>SHA-256 (hex en minúsculas) del JSON canónico de la política sin su hash.</summary>
    public static string HashOf(CompiledPolicy policy) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(policy with { Hash = null }, Canonical))));

    private static string Key(IReadOnlyList<PolicyWindow> windows) =>
        string.Join(';', windows.Select(w => $"{w.Days},{w.From},{w.To}"));

    private static DateTimeOffset? Max(DateTimeOffset? a, DateTimeOffset? b) => a is null ? b : b is null ? a : a > b ? a : b;

    private static DateTimeOffset? Min(DateTimeOffset? a, DateTimeOffset? b) => a is null ? b : b is null ? a : a < b ? a : b;
}
