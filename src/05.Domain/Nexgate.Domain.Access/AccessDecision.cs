using Nexgate.Service.Access.Contracts;

namespace Nexgate.Domain.Access;

/// <summary>
/// Decisión de acceso sobre la política compilada (design de add-access-evaluator, decisión 7). Es el algoritmo que
/// comparten el backend y el firmware: usa solo la política, el hash de la credencial presentada y el instante.
/// </summary>
public static class AccessDecision
{
    public static AccessDecisionResult Evaluate(CompiledPolicy policy, string credentialHash, DateTimeOffset at)
    {
        var instant = at.ToUnixTimeSeconds();

        // 1. La credencial tiene que estar en la política y vigente.
        var index = -1;
        for (var i = 0; i < policy.Credentials.Count; i++)
        {
            if (string.Equals(policy.Credentials[i].Hash, credentialHash, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        if (index < 0 || !Within(instant, policy.Credentials[index].From, policy.Credentials[index].To))
        {
            return Deny(AccessReasonNames.CredentialInvalid);
        }

        // 2. Hora local con la tabla de desplazamientos (sin base de zonas horarias).
        var (day, minute) = LocalTime(policy.Offsets, instant);

        // 3. Entradas de la credencial que aplican en el instante: vigencia y ventana.
        var allowed = false;
        foreach (var entry in policy.Entries)
        {
            if (entry.Credential != index || !Within(instant, entry.From, entry.To) || !InWindow(policy, entry.Window, day, minute))
            {
                continue;
            }

            // 4. Gana siempre la denegación.
            if (entry.Deny)
            {
                return Deny(AccessReasonNames.ExplicitDeny);
            }

            // 5. Un permiso con usos restantes (o sin límite).
            if (entry.Uses is null or > 0)
            {
                allowed = true;
            }
        }

        // 6. Sin permiso que aplique.
        return allowed
            ? new AccessDecisionResult(AccessDecisionNames.Allow, AccessReasonNames.Allowed)
            : Deny(AccessReasonNames.NoPermission);
    }

    /// <summary>Día (0 = lunes … 6 = domingo) y minuto del día locales.</summary>
    public static (int Day, int Minute) LocalTime(IReadOnlyList<PolicyOffset> offsets, long instant)
    {
        var seconds = 0;
        foreach (var offset in offsets)
        {
            if (offset.From is null || offset.From <= instant)
            {
                seconds = offset.Seconds;
            }
        }

        var local = DateTimeOffset.FromUnixTimeSeconds(instant + seconds).UtcDateTime;
        return (((int)local.DayOfWeek + 6) % 7, local.Hour * 60 + local.Minute);
    }

    private static bool Within(long instant, long? from, long? to) => (from is null || instant >= from) && (to is null || instant < to);

    private static bool InWindow(CompiledPolicy policy, int? window, int day, int minute)
    {
        if (window is null)
        {
            return true;
        }

        if (window < 0 || window >= policy.Windows.Count)
        {
            return false;
        }

        foreach (var w in policy.Windows[window.Value])
        {
            if ((w.Days & (1 << day)) != 0 && minute >= w.From && minute < w.To)
            {
                return true;
            }
        }

        return false;
    }

    private static AccessDecisionResult Deny(string reason) => new(AccessDecisionNames.Deny, reason);
}
