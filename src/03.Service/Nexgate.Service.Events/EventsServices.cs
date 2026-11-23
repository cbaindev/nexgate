using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexgate.Data.Events;
using Nexgate.Domain.Events;
using Nexgate.Service.Access.Contracts;
using Nexgate.Service.Events.Contracts;
using Nexgate.Service.Spaces.Contracts;
using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Service.Events;

internal static class EventsMapping
{
    public static AccessEventDto ToDto(AccessEvent e) =>
        new(e.Id, e.SiteId, e.DeviceId, e.AccessPointId, e.OccurredAt, e.ReceivedAt, e.Decision, e.Reason, e.CredentialId, e.SubjectId,
            e.PolicyVersion);

    /// <summary>Motivos válidos de cada decisión (los de la decisión de acceso, D-13).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ReasonsByDecision = new Dictionary<string, string[]>
    {
        [AccessDecisionNames.Allow] = [AccessReasonNames.Allowed],
        [AccessDecisionNames.Deny] = [AccessReasonNames.CredentialInvalid, AccessReasonNames.ExplicitDeny, AccessReasonNames.NoPermission]
    };
}

/// <summary>
/// Ingesta idempotente de eventos y descuento de usos (design de add-device-sync-protocol, decisiones 6 y 7). El lote se
/// valida entero antes de guardar nada. Los hashes de credenciales nunca se guardan ni se registran.
/// </summary>
internal sealed class AccessEventIngestion(
    IAccessEventStore store,
    ISpaceDirectory spaces,
    ICredentialLookup credentials,
    IGrantUses uses,
    TimeProvider clock) : IAccessEventIngestion
{
    private const string UnknownPoint = "El punto de acceso no existe en el sitio del dispositivo.";

    public async Task<EventsResult<IngestResult>> IngestAsync(Guid deviceId, Guid siteId, IReadOnlyList<AccessEventInput>? events,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (events is null || events.Count is 0 or > AccessEventLimits.MaxBatch)
        {
            errors["Events"] = [$"El lote tiene entre 1 y {AccessEventLimits.MaxBatch} eventos."];
            return EventsResult<IngestResult>.Invalid(errors);
        }

        var points = new Dictionary<Guid, bool>();
        for (var i = 0; i < events.Count; i++)
        {
            var e = events[i];
            var prefix = $"Events[{i}].";
            if (e is null)
            {
                errors[$"Events[{i}]"] = ["El evento es obligatorio."];
                continue;
            }

            if (e.Id is null || e.Id == Guid.Empty)
            {
                errors[prefix + "Id"] = ["El identificador del evento es obligatorio."];
            }

            if (e.OccurredAt is null)
            {
                errors[prefix + "OccurredAt"] = ["El instante es obligatorio."];
            }

            if (e.Decision is null || !EventsMapping.ReasonsByDecision.TryGetValue(e.Decision, out var reasons))
            {
                errors[prefix + "Decision"] = ["Decisión inválida: allow o deny."];
            }
            else if (e.Reason is null || !reasons.Contains(e.Reason))
            {
                errors[prefix + "Reason"] = ["Motivo inválido para la decisión."];
            }

            if (e.Credential is { Length: 0 or > 100 })
            {
                errors[prefix + "Credential"] = ["El hash de la credencial es inválido."];
            }

            if (e.PolicyVersion is < 0)
            {
                errors[prefix + "PolicyVersion"] = ["La versión de política no puede ser negativa."];
            }

            if (e.AccessPointId is not { } pointId || pointId == Guid.Empty)
            {
                errors[prefix + "AccessPointId"] = [UnknownPoint];
            }
            else
            {
                if (!points.TryGetValue(pointId, out var valid))
                {
                    // De otro sitio, de otra organización o inexistente: la misma respuesta.
                    valid = await spaces.GetAccessPointAsync(pointId, cancellationToken) is { } point && point.SiteId == siteId;
                    points[pointId] = valid;
                }

                if (!valid)
                {
                    errors[prefix + "AccessPointId"] = [UnknownPoint];
                }
            }
        }

        if (errors.Count > 0)
        {
            return EventsResult<IngestResult>.Invalid(errors);
        }

        var now = clock.GetUtcNow();
        var resolved = new Dictionary<string, CredentialReference?>(StringComparer.Ordinal);
        var accepted = 0;
        var duplicates = new List<Guid>();
        foreach (var e in events)
        {
            CredentialReference? credential = null;
            if (e.Credential is { } hash && !resolved.TryGetValue(hash, out credential))
            {
                credential = await credentials.FindByHashAsync(siteId, hash, cancellationToken);
                resolved[hash] = credential;
            }

            var consumesUse = credential is not null && e.Decision == AccessDecisionNames.Allow;
            var stored = AccessEvent.Create(siteId, deviceId, e.Id!.Value, e.AccessPointId!.Value, e.OccurredAt!.Value.ToUniversalTime(), now,
                e.Decision!, e.Reason!, credential?.CredentialId, credential?.SubjectId, e.PolicyVersion, consumesUse);
            if (!await store.TryInsertAsync(stored, cancellationToken))
            {
                duplicates.Add(e.Id.Value);
                continue;
            }

            accepted++;
            if (consumesUse)
            {
                await SettleAsync(stored, cancellationToken);
            }
        }

        // Un reenvío completa el descuento que quedó pendiente si la solicitud anterior se cortó en el medio.
        if (duplicates.Count > 0)
        {
            foreach (var pending in await store.ListUnsettledAsync(deviceId, duplicates, cancellationToken))
            {
                await SettleAsync(pending, cancellationToken);
            }
        }

        return EventsResult<IngestResult>.Ok(new IngestResult(accepted, duplicates.Count));
    }

    private async Task SettleAsync(AccessEvent e, CancellationToken cancellationToken)
    {
        if (e.CredentialId is { } credentialId)
        {
            await uses.ConsumeAsync(e.SiteId, e.AccessPointId, credentialId, e.OccurredAt, cancellationToken);
        }

        await store.MarkSettledAsync(e.Id, cancellationToken);
    }
}

internal sealed class AccessEventService(IAccessEventStore store, ISiteDirectory sites) : IAccessEventService
{
    public async Task<EventsResult<AccessEventPage>> ListAsync(AccessEventQuery query, CancellationToken cancellationToken)
    {
        if (!await sites.ExistsAsync(query.SiteId, cancellationToken))
        {
            return EventsResult<AccessEventPage>.NotFound();
        }

        var errors = new Dictionary<string, string[]>();
        DateTimeOffset? afterAt = null;
        Guid? afterId = null;
        if (query.Cursor is not null)
        {
            if (EventCursor.TryDecode(query.Cursor, out var at, out var id))
            {
                (afterAt, afterId) = (at, id);
            }
            else
            {
                errors["Cursor"] = ["Cursor inválido."];
            }
        }

        var limit = query.Limit ?? AccessEventLimits.DefaultPageSize;
        if (limit is < 1 or > AccessEventLimits.MaxPageSize)
        {
            errors["Limit"] = [$"El tamaño de página va de 1 a {AccessEventLimits.MaxPageSize}."];
        }

        if (query.From is { } from && query.To is { } to && to <= from)
        {
            errors["To"] = ["El fin del rango debe ser posterior al comienzo."];
        }

        if (errors.Count > 0)
        {
            return EventsResult<AccessEventPage>.Invalid(errors);
        }

        var rows = await store.ListAsync(new EventFilter(query.SiteId, query.AccessPointId, query.SubjectId,
            query.From?.ToUniversalTime(), query.To?.ToUniversalTime(), afterAt, afterId, limit + 1), cancellationToken);
        var page = rows.Take(limit).ToList();
        var next = rows.Count > limit ? EventCursor.Encode(page[^1].OccurredAt, page[^1].Id) : null;
        return EventsResult<AccessEventPage>.Ok(new AccessEventPage(page.Select(EventsMapping.ToDto).ToList(), next));
    }
}

/// <summary>Módulo Events: por ahora, eventos de acceso de los dispositivos. Requiere Tenancy, Spaces y Access.</summary>
public static class EventsServiceCollectionExtensions
{
    public static IServiceCollection AddEventsService(this IServiceCollection services)
    {
        services.AddEventsData();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAccessEventIngestion, AccessEventIngestion>();
        services.AddScoped<IAccessEventService, AccessEventService>();
        return services;
    }
}
