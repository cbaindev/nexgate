namespace Nexgate.Domain.Events;

/// <summary>
/// Una pasada que decidió un dispositivo (design de add-device-sync-protocol, decisión 6). Se identifica por el
/// dispositivo y el identificador que generó él (idempotencia). <see cref="UsesSettled"/> indica que el descuento de usos
/// ya se resolvió (o no correspondía).
/// </summary>
public sealed class AccessEvent
{
    private AccessEvent()
    {
        Decision = string.Empty;
        Reason = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid SiteId { get; private set; }

    public Guid DeviceId { get; private set; }

    public Guid DeviceEventId { get; private set; }

    public Guid AccessPointId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public string Decision { get; private set; }

    public string Reason { get; private set; }

    public Guid? CredentialId { get; private set; }

    public Guid? SubjectId { get; private set; }

    public int? PolicyVersion { get; private set; }

    public bool UsesSettled { get; private set; }

    /// <param name="consumesUse">Pasada permitida de una credencial conocida: queda pendiente de descontar un uso.</param>
    public static AccessEvent Create(Guid siteId, Guid deviceId, Guid deviceEventId, Guid accessPointId, DateTimeOffset occurredAt,
        DateTimeOffset receivedAt, string decision, string reason, Guid? credentialId, Guid? subjectId, int? policyVersion, bool consumesUse) =>
        new()
        {
            Id = Guid.CreateVersion7(receivedAt),
            SiteId = siteId,
            DeviceId = deviceId,
            DeviceEventId = deviceEventId,
            AccessPointId = accessPointId,
            OccurredAt = occurredAt,
            ReceivedAt = receivedAt,
            Decision = decision,
            Reason = reason,
            CredentialId = credentialId,
            SubjectId = subjectId,
            PolicyVersion = policyVersion,
            UsesSettled = !consumesUse
        };
}

/// <summary>Cursor de la consulta de eventos: el último (instante, id) de la página, opaco para el cliente.</summary>
public static class EventCursor
{
    public static string Encode(DateTimeOffset occurredAt, Guid id) =>
        Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{occurredAt.UtcTicks}:{id:N}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TryDecode(string? cursor, out DateTimeOffset occurredAt, out Guid id)
    {
        occurredAt = default;
        id = default;
        if (string.IsNullOrEmpty(cursor) || cursor.Length > 100)
        {
            return false;
        }

        try
        {
            var s = cursor.Replace('-', '+').Replace('_', '/');
            var text = System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=')));
            var parts = text.Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks) || !Guid.TryParseExact(parts[1], "N", out id)
                || ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
            {
                return false;
            }

            occurredAt = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
