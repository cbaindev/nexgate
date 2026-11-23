namespace Nexgate.Service.Events.Contracts;

/// <summary>
/// Un evento de acceso de un dispositivo (design de add-device-sync-protocol, decisión 6). Referencia credencial y sujeto
/// por identificador; nunca lleva el hash de la credencial.
/// </summary>
public sealed record AccessEventDto(
    Guid Id,
    Guid SiteId,
    Guid DeviceId,
    Guid AccessPointId,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    string Decision,
    string Reason,
    Guid? CredentialId,
    Guid? SubjectId,
    int? PolicyVersion);

/// <summary>Un evento tal como lo envía el dispositivo. <see cref="Credential"/> es el hash leído (base64url), si lo hubo.</summary>
public sealed record AccessEventInput(
    Guid? Id,
    DateTimeOffset? OccurredAt,
    Guid? AccessPointId,
    string? Credential,
    string? Decision,
    string? Reason,
    int? PolicyVersion);

public sealed record IngestResult(int Accepted, int Duplicates);

/// <summary>Una página de eventos y el cursor de la siguiente (null si no hay más).</summary>
public sealed record AccessEventPage(IReadOnlyList<AccessEventDto> Items, string? NextCursor);

public sealed record AccessEventQuery(
    Guid SiteId,
    Guid? AccessPointId,
    Guid? SubjectId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Cursor,
    int? Limit);

public static class AccessEventLimits
{
    public const int MaxBatch = 100;
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;
}

public enum EventsErrorKind
{
    Validation,
    NotFound
}

/// <summary>Un error esperado; en validación, cada campo con su mensaje.</summary>
public sealed record EventsError(EventsErrorKind Kind, IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record EventsResult<T>(T? Value, EventsError? Error)
{
    public static EventsResult<T> Ok(T value) => new(value, null);

    public static EventsResult<T> NotFound() => new(default, new EventsError(EventsErrorKind.NotFound));

    public static EventsResult<T> Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(default, new EventsError(EventsErrorKind.Validation, errors));
}

/// <summary>Ingesta idempotente de eventos del dispositivo de la solicitud (con su organización activa).</summary>
public interface IAccessEventIngestion
{
    Task<EventsResult<IngestResult>> IngestAsync(Guid deviceId, Guid siteId, IReadOnlyList<AccessEventInput>? events,
        CancellationToken cancellationToken);
}

/// <summary>Consulta de eventos de los sitios de la organización activa.</summary>
public interface IAccessEventService
{
    /// <summary>Del más reciente al más antiguo; NotFound si el sitio no existe en la organización activa.</summary>
    Task<EventsResult<AccessEventPage>> ListAsync(AccessEventQuery query, CancellationToken cancellationToken);
}
