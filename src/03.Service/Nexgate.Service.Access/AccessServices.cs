using NodaTime;
using Nexgate.Data.Access;
using Nexgate.Domain.Access;
using Nexgate.Service.Access.Contracts;
using Nexgate.Service.Occupancy.Contracts;
using Nexgate.Service.Spaces.Contracts;
using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Service.Access;

internal static class AccessMapping
{
    public static ScheduleDto ToDto(Schedule schedule) =>
        new(schedule.Id, schedule.SiteId, schedule.Name,
            schedule.Windows.Select(w => new ScheduleWindowDto(w.DayNameList(), ScheduleWindow.FormatTime(w.From), ScheduleWindow.FormatTime(w.To)))
                .ToList());

    public static CredentialDto ToDto(Credential credential) =>
        new(credential.Id, credential.SiteId, credential.SubjectId, AccessNames.ToName(credential.Type), credential.ValidFrom, credential.ValidUntil);

    public static GrantDto ToDto(Grant grant) =>
        new(grant.Id, grant.SiteId, grant.SubjectId, AccessNames.ToName(grant.TargetType), grant.TargetId, grant.ScheduleId,
            grant.ValidFrom, grant.ValidUntil, AccessNames.ToName(grant.Effect), AccessNames.ToName(grant.Origin), grant.RemainingUses);

    public static AccessError Conflict(string code, string message) => new(AccessErrorKind.Conflict, null, message, code);
}

internal sealed class ScheduleService(IScheduleStore store, ISiteDirectory sites, TimeProvider clock) : IScheduleService
{
    private static readonly AccessError NameConflict =
        AccessMapping.Conflict(AccessErrorCodes.ScheduleNameConflict, "Ya existe un horario con ese nombre en el sitio.");

    public async Task<AccessResult<IReadOnlyList<ScheduleDto>>> ListAsync(Guid siteId, CancellationToken cancellationToken) =>
        await sites.ExistsAsync(siteId, cancellationToken)
            ? AccessResult<IReadOnlyList<ScheduleDto>>.Ok((await store.ListBySiteAsync(siteId, cancellationToken)).Select(AccessMapping.ToDto).ToList())
            : AccessResult<IReadOnlyList<ScheduleDto>>.NotFound();

    public async Task<AccessResult<ScheduleDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await store.FindAsync(id, cancellationToken) is { } schedule
            ? AccessResult<ScheduleDto>.Ok(AccessMapping.ToDto(schedule))
            : AccessResult<ScheduleDto>.NotFound();

    public async Task<AccessResult<ScheduleDto>> CreateAsync(Guid siteId, ScheduleInput input, CancellationToken cancellationToken)
    {
        if (!await sites.ExistsAsync(siteId, cancellationToken))
        {
            return AccessResult<ScheduleDto>.NotFound();
        }

        Schedule schedule;
        try
        {
            schedule = Schedule.Create(siteId, input.Name, ParseWindows(input.Windows), clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return AccessResult<ScheduleDto>.Invalid(e.Field, e.Message);
        }

        if (await store.NameInUseAsync(siteId, schedule.Name, null, cancellationToken))
        {
            return AccessResult<ScheduleDto>.Fail(NameConflict);
        }

        store.Add(schedule);
        return await store.SaveChangesAsync(cancellationToken)
            ? AccessResult<ScheduleDto>.Ok(AccessMapping.ToDto(schedule))
            : AccessResult<ScheduleDto>.Fail(NameConflict);
    }

    public async Task<AccessResult<ScheduleDto>> UpdateAsync(Guid id, ScheduleInput input, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } schedule)
        {
            return AccessResult<ScheduleDto>.NotFound();
        }

        try
        {
            schedule.Update(input.Name, ParseWindows(input.Windows), clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return AccessResult<ScheduleDto>.Invalid(e.Field, e.Message);
        }

        if (await store.NameInUseAsync(schedule.SiteId, schedule.Name, schedule.Id, cancellationToken))
        {
            return AccessResult<ScheduleDto>.Fail(NameConflict);
        }

        return await store.SaveChangesAsync(cancellationToken)
            ? AccessResult<ScheduleDto>.Ok(AccessMapping.ToDto(schedule))
            : AccessResult<ScheduleDto>.Fail(NameConflict);
    }

    public async Task<AccessResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } schedule)
        {
            return AccessResult<Guid>.NotFound();
        }

        var inUse = AccessMapping.Conflict(AccessErrorCodes.ScheduleInUse, "El horario lo usa un otorgamiento.");
        if (await store.InUseAsync(id, cancellationToken))
        {
            return AccessResult<Guid>.Fail(inUse);
        }

        store.Remove(schedule);
        return await store.SaveChangesAsync(cancellationToken) ? AccessResult<Guid>.Ok(id) : AccessResult<Guid>.Fail(inUse);
    }

    private static List<ScheduleWindow> ParseWindows(IReadOnlyList<ScheduleWindowDto>? windows) =>
        (windows ?? []).Select(w => ScheduleWindow.Parse(w.Days, w.From, w.To)).ToList();
}

internal sealed class CredentialService(
    ICredentialStore store,
    ISiteDirectory sites,
    ISubjectDirectory subjects,
    CredentialKey key,
    TimeProvider clock) : ICredentialService
{
    private static readonly AccessError Duplicate =
        AccessMapping.Conflict(AccessErrorCodes.CredentialConflict, "Ya existe una credencial igual en el sitio.");

    public async Task<AccessResult<IReadOnlyList<CredentialDto>>> ListAsync(Guid siteId, Guid? subjectId, CancellationToken cancellationToken) =>
        await sites.ExistsAsync(siteId, cancellationToken)
            ? AccessResult<IReadOnlyList<CredentialDto>>.Ok((await store.ListBySiteAsync(siteId, subjectId, cancellationToken))
                .Select(AccessMapping.ToDto).ToList())
            : AccessResult<IReadOnlyList<CredentialDto>>.NotFound();

    public async Task<AccessResult<CredentialDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await store.FindAsync(id, cancellationToken) is { } credential
            ? AccessResult<CredentialDto>.Ok(AccessMapping.ToDto(credential))
            : AccessResult<CredentialDto>.NotFound();

    public async Task<AccessResult<CredentialDto>> CreateAsync(Guid siteId, CredentialInput input, CancellationToken cancellationToken)
    {
        if (!await sites.ExistsAsync(siteId, cancellationToken))
        {
            return AccessResult<CredentialDto>.NotFound();
        }

        if (!AccessNames.TryParseCredentialType(input.Type, out var type))
        {
            return AccessResult<CredentialDto>.Invalid(nameof(CredentialInput.Type), "Tipo de credencial inválido: rf, card o pin.");
        }

        if (await subjects.GetSubjectAsync(input.SubjectId, cancellationToken) is not { } subject || subject.SiteId != siteId)
        {
            return AccessResult<CredentialDto>.Invalid(nameof(CredentialInput.SubjectId), "El sujeto no existe en el sitio.");
        }

        Credential credential;
        try
        {
            var hash = CredentialHash.Compute(key.Master, siteId, type, input.Value);
            credential = Credential.Create(siteId, input.SubjectId, type, hash, input.ValidFrom, input.ValidUntil, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return AccessResult<CredentialDto>.Invalid(e.Field, e.Message);
        }

        if (await store.ExistsAsync(siteId, type, credential.ValueHash, cancellationToken))
        {
            return AccessResult<CredentialDto>.Fail(Duplicate);
        }

        store.Add(credential);
        return await store.SaveChangesAsync(cancellationToken)
            ? AccessResult<CredentialDto>.Ok(AccessMapping.ToDto(credential))
            : AccessResult<CredentialDto>.Fail(Duplicate);
    }

    public async Task<AccessResult<CredentialDto>> UpdateAsync(Guid id, CredentialUpdate update, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } credential)
        {
            return AccessResult<CredentialDto>.NotFound();
        }

        try
        {
            credential.SetValidity(update.ValidFrom, update.ValidUntil);
        }
        catch (DomainValidationException e)
        {
            return AccessResult<CredentialDto>.Invalid(e.Field, e.Message);
        }

        await store.SaveChangesAsync(cancellationToken);
        return AccessResult<CredentialDto>.Ok(AccessMapping.ToDto(credential));
    }

    public async Task<AccessResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } credential)
        {
            return AccessResult<Guid>.NotFound();
        }

        store.Remove(credential);
        await store.SaveChangesAsync(cancellationToken);
        return AccessResult<Guid>.Ok(id);
    }
}

internal sealed class GrantService(
    IGrantStore store,
    IScheduleStore schedules,
    ISiteDirectory sites,
    ISubjectDirectory subjects,
    ISpaceDirectory spaces,
    TimeProvider clock) : IGrantService
{
    public async Task<AccessResult<IReadOnlyList<GrantDto>>> ListAsync(Guid siteId, Guid? subjectId, CancellationToken cancellationToken) =>
        await sites.ExistsAsync(siteId, cancellationToken)
            ? AccessResult<IReadOnlyList<GrantDto>>.Ok((await store.ListBySiteAsync(siteId, subjectId, cancellationToken))
                .Select(AccessMapping.ToDto).ToList())
            : AccessResult<IReadOnlyList<GrantDto>>.NotFound();

    public async Task<AccessResult<GrantDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await store.FindAsync(id, cancellationToken) is { } grant
            ? AccessResult<GrantDto>.Ok(AccessMapping.ToDto(grant))
            : AccessResult<GrantDto>.NotFound();

    public async Task<AccessResult<GrantDto>> CreateAsync(Guid siteId, GrantInput input, Guid createdBy, CancellationToken cancellationToken)
    {
        if (!await sites.ExistsAsync(siteId, cancellationToken))
        {
            return AccessResult<GrantDto>.NotFound();
        }

        if (!AccessNames.TryParseTargetType(input.TargetType, out var targetType))
        {
            return AccessResult<GrantDto>.Invalid(nameof(GrantInput.TargetType), "Tipo de destino inválido: space o access_point.");
        }

        if (!AccessNames.TryParseEffect(input.Effect, out var effect))
        {
            return AccessResult<GrantDto>.Invalid(nameof(GrantInput.Effect), "Efecto inválido: allow o deny.");
        }

        if (await subjects.GetSubjectAsync(input.SubjectId, cancellationToken) is not { } subject || subject.SiteId != siteId)
        {
            return AccessResult<GrantDto>.Invalid(nameof(GrantInput.SubjectId), "El sujeto no existe en el sitio.");
        }

        var targetSite = targetType == GrantTargetType.Space
            ? (await spaces.GetSpaceAsync(input.TargetId, cancellationToken))?.SiteId
            : (await spaces.GetAccessPointAsync(input.TargetId, cancellationToken))?.SiteId;
        if (targetSite != siteId)
        {
            return AccessResult<GrantDto>.Invalid(nameof(GrantInput.TargetId), "El destino no existe en el sitio.");
        }

        if (input.ScheduleId is { } scheduleId
            && (await schedules.FindAsync(scheduleId, cancellationToken))?.SiteId != siteId)
        {
            return AccessResult<GrantDto>.Invalid(nameof(GrantInput.ScheduleId), "El horario no existe en el sitio.");
        }

        Grant grant;
        try
        {
            grant = Grant.Create(siteId, input.SubjectId, targetType, input.TargetId, input.ScheduleId, input.ValidFrom, input.ValidUntil,
                effect, input.MaxUses, createdBy, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return AccessResult<GrantDto>.Invalid(e.Field, e.Message);
        }

        store.Add(grant);
        await store.SaveChangesAsync(cancellationToken);
        return AccessResult<GrantDto>.Ok(AccessMapping.ToDto(grant));
    }

    public async Task<AccessResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } grant)
        {
            return AccessResult<Guid>.NotFound();
        }

        store.Remove(grant);
        await store.SaveChangesAsync(cancellationToken);
        return AccessResult<Guid>.Ok(id);
    }
}

/// <summary>
/// Compila la política de un punto de acceso (cobertura por subárbol, design decisión 5) y simula pasadas con la misma
/// decisión que usa el firmware. La simulación no registra eventos ni descuenta usos.
/// </summary>
internal sealed class AccessPolicyService(
    IGrantStore grants,
    IScheduleStore schedules,
    ICredentialStore credentials,
    ISiteDirectory sites,
    ISpaceDirectory spaces,
    CredentialKey key,
    TimeProvider clock) : IAccessPolicyService
{
    public const int OffsetHorizonDays = 400;

    public async Task<AccessResult<CompiledPolicy>> CompileAsync(Guid accessPointId, CancellationToken cancellationToken)
    {
        if (await spaces.GetAccessPointAsync(accessPointId, cancellationToken) is not { } point
            || await sites.GetTimeZoneAsync(point.SiteId, cancellationToken) is not { } timeZone)
        {
            return AccessResult<CompiledPolicy>.NotFound();
        }

        var now = clock.GetUtcNow();
        var subtree = await spaces.GetSubtreeIdsAsync(point.ToSpaceId, cancellationToken) ?? [];
        var covering = await grants.ListCoveringAsync(point.SiteId, subtree, point.Id, cancellationToken);
        var scheduleIds = covering.Where(g => g.ScheduleId is not null).Select(g => g.ScheduleId!.Value).Distinct().ToList();
        var windows = scheduleIds.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<ScheduleWindow>>()
            : (await schedules.FindManyAsync(scheduleIds, cancellationToken)).ToDictionary(s => s.Id, s => s.Windows);
        var siteCredentials = await credentials.ListBySiteAsync(point.SiteId, null, cancellationToken);

        var policy = PolicyCompiler.Compile(
            point.SiteId,
            point.Id,
            timeZone,
            Offsets(timeZone, now),
            siteCredentials
                .Select(c => new CompilableCredential(c.SubjectId, CredentialHash.ToPolicy(c.ValueHash), c.ValidFrom, c.ValidUntil))
                .ToList(),
            covering.Select(g => new CompilableGrant(g.SubjectId, g.ScheduleId is { } s ? windows.GetValueOrDefault(s, []) : null,
                    g.ValidFrom, g.ValidUntil, g.Effect == GrantEffect.Deny, g.RemainingUses))
                .ToList(),
            now);
        return AccessResult<CompiledPolicy>.Ok(policy);
    }

    public async Task<AccessResult<SimulationResultDto>> SimulateAsync(Guid accessPointId, string credentialType, string credentialValue,
        DateTimeOffset? at, CancellationToken cancellationToken)
    {
        if (!AccessNames.TryParseCredentialType(credentialType, out var type))
        {
            return AccessResult<SimulationResultDto>.Invalid("CredentialType", "Tipo de credencial inválido: rf, card o pin.");
        }

        var compiled = await CompileAsync(accessPointId, cancellationToken);
        if (compiled.Value is not { } policy)
        {
            return AccessResult<SimulationResultDto>.NotFound();
        }

        string hash;
        try
        {
            hash = CredentialHash.ToPolicy(CredentialHash.Compute(key.Master, policy.SiteId, type, credentialValue));
        }
        catch (DomainValidationException e)
        {
            return AccessResult<SimulationResultDto>.Invalid("CredentialValue", e.Message);
        }

        var decision = AccessDecision.Evaluate(policy, hash, at ?? clock.GetUtcNow());
        return AccessResult<SimulationResultDto>.Ok(new SimulationResultDto(decision.Decision, decision.Reason, policy.Hash!));
    }

    /// <summary>
    /// Desplazamiento vigente y transiciones de los próximos 400 días (tzdb incluida, no del sistema operativo).
    /// </summary>
    public static IReadOnlyList<PolicyOffset> Offsets(string timeZone, DateTimeOffset now)
    {
        var zone = DateTimeZoneProviders.Tzdb[timeZone];
        var start = Instant.FromDateTimeOffset(now);
        var intervals = zone.GetZoneIntervals(start, start + Duration.FromDays(OffsetHorizonDays)).ToList();
        return intervals
            .Select((interval, index) => new PolicyOffset(
                index == 0 ? null : interval.Start.ToUnixTimeSeconds(),
                interval.WallOffset.Seconds))
            .ToList();
    }
}

internal sealed class SiteCredentialKeys(CredentialKey key) : ISiteCredentialKeys
{
    public byte[] GetSiteKey(Guid siteId) => CredentialHash.SiteKey(key.Master, siteId);
}

internal sealed class CredentialLookup(ICredentialStore credentials) : ICredentialLookup
{
    public async Task<CredentialReference?> FindByHashAsync(Guid siteId, string policyHash, CancellationToken cancellationToken) =>
        CredentialHash.TryFromPolicy(policyHash) is { Length: 32 } hash
        && await credentials.FindByHashAsync(siteId, hash, cancellationToken) is { } credential
            ? new CredentialReference(credential.Id, credential.SubjectId)
            : null;
}

/// <summary>
/// Descuento de usos por eventos de dispositivos (design de add-device-sync-protocol, decisión 7). Con varios dispositivos
/// sin red, el límite es eventual: cada uno decidió con su política; acá solo se evita bajar de cero.
/// </summary>
internal sealed class GrantUses(
    IGrantStore grants,
    IScheduleStore schedules,
    ICredentialStore credentials,
    ISiteDirectory sites,
    ISpaceDirectory spaces) : IGrantUses
{
    public async Task<bool> ConsumeAsync(Guid siteId, Guid accessPointId, Guid credentialId, DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        if (await credentials.FindAsync(credentialId, cancellationToken) is not { } credential || credential.SiteId != siteId
            || await spaces.GetAccessPointAsync(accessPointId, cancellationToken) is not { } point || point.SiteId != siteId
            || await sites.GetTimeZoneAsync(siteId, cancellationToken) is not { } timeZone)
        {
            return false;
        }

        var subtree = await spaces.GetSubtreeIdsAsync(point.ToSpaceId, cancellationToken) ?? [];
        var candidates = (await grants.ListCoveringAsync(siteId, subtree, point.Id, cancellationToken))
            .Where(g => g.SubjectId == credential.SubjectId && g.Effect == GrantEffect.Allow && Valid(g, occurredAt))
            .ToList();
        var scheduleIds = candidates.Where(g => g.ScheduleId is not null).Select(g => g.ScheduleId!.Value).Distinct().ToList();
        var windows = scheduleIds.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<ScheduleWindow>>()
            : (await schedules.FindManyAsync(scheduleIds, cancellationToken)).ToDictionary(s => s.Id, s => s.Windows);
        var (day, minute) = LocalTime(timeZone, occurredAt);
        var applicable = candidates
            .Where(g => g.ScheduleId is not { } s
                        || windows.GetValueOrDefault(s, []).Any(w => (w.Days & (1 << day)) != 0 && w.From <= minute && minute < w.To))
            .ToList();

        if (applicable.Any(g => g.RemainingUses is null))
        {
            return true;
        }

        foreach (var grant in applicable.Where(g => g.RemainingUses > 0)
                     .OrderBy(g => g.ValidUntil ?? DateTimeOffset.MaxValue).ThenBy(g => g.CreatedAt).ThenBy(g => g.Id))
        {
            if (await grants.ConsumeUseAsync(grant.Id, cancellationToken))
            {
                break;
            }
        }

        return true;
    }

    private static bool Valid(Grant grant, DateTimeOffset at) =>
        (grant.ValidFrom is not { } from || from <= at) && (grant.ValidUntil is not { } until || at < until);

    /// <summary>Día (0 = lunes) y minuto locales del instante en la zona del sitio.</summary>
    internal static (int Day, int Minute) LocalTime(string timeZone, DateTimeOffset at)
    {
        var local = Instant.FromDateTimeOffset(at).InZone(DateTimeZoneProviders.Tzdb[timeZone]);
        return ((int)local.DayOfWeek - 1, local.Hour * 60 + local.Minute);
    }
}

