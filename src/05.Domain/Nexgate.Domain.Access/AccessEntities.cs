using System.Globalization;
using Nexgate.Service.Access.Contracts;

namespace Nexgate.Domain.Access;

/// <summary>Una regla del dominio no se cumple. El campo usa el nombre de la propiedad pública de la solicitud.</summary>
public sealed class DomainValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

public enum CredentialType
{
    Rf,
    Card,
    Pin
}

public enum GrantEffect
{
    Allow,
    Deny
}

public enum GrantTargetType
{
    Space,
    AccessPoint
}

public enum GrantOrigin
{
    Manual
}

/// <summary>Una franja horaria: máscara de días (bit 0 = lunes) y minutos locales [from, to).</summary>
public sealed record ScheduleWindow(int Days, int From, int To)
{
    public const int MinutesPerDay = 24 * 60;

    public static ScheduleWindow Parse(IReadOnlyCollection<string>? days, string? from, string? to)
    {
        if (days is null || days.Count == 0)
        {
            throw new DomainValidationException("Windows", "Cada ventana necesita al menos un día.");
        }

        var mask = 0;
        foreach (var day in days)
        {
            var index = DayNames.All.ToList().IndexOf(day);
            if (index < 0)
            {
                throw new DomainValidationException("Windows", $"Día inválido: {day}.");
            }

            mask |= 1 << index;
        }

        var start = ParseTime(from);
        var end = ParseTime(to);
        if (end <= start)
        {
            throw new DomainValidationException("Windows", "El fin de cada ventana debe ser posterior a su inicio.");
        }

        return new ScheduleWindow(mask, start, end);
    }

    public IReadOnlyList<string> DayNameList() =>
        DayNames.All.Where((_, index) => (Days & (1 << index)) != 0).ToList();

    public static string FormatTime(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

    private static int ParseTime(string? value)
    {
        var parts = value?.Split(':');
        if (parts is not { Length: 2 }
            || parts[0].Length != 2 || parts[1].Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || minutes > 59
            || hours * 60 + minutes > MinutesPerDay)
        {
            throw new DomainValidationException("Windows", "Las horas van en formato HH:mm, de 00:00 a 24:00.");
        }

        return hours * 60 + minutes;
    }
}

/// <summary>Horario de un sitio: franjas por día de la semana en la zona horaria del sitio.</summary>
public sealed class Schedule
{
    private Schedule()
    {
        Name = string.Empty;
        Windows = [];
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid SiteId { get; private set; }

    public string Name { get; private set; }

    public IReadOnlyList<ScheduleWindow> Windows { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Schedule Create(Guid siteId, string name, IReadOnlyList<ScheduleWindow> windows, DateTimeOffset now)
    {
        var schedule = new Schedule { Id = Guid.CreateVersion7(now), SiteId = siteId, CreatedAt = now };
        schedule.Update(name, windows, now);
        return schedule;
    }

    public void Update(string name, IReadOnlyList<ScheduleWindow> windows, DateTimeOffset now)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > AccessLimits.ScheduleNameMaxLength)
        {
            throw new DomainValidationException(nameof(Name), $"El nombre es obligatorio y no puede superar {AccessLimits.ScheduleNameMaxLength} caracteres.");
        }

        if (windows.Count is 0 or > AccessLimits.MaxWindows)
        {
            throw new DomainValidationException(nameof(Windows), $"Un horario tiene entre 1 y {AccessLimits.MaxWindows} ventanas.");
        }

        Name = trimmed;
        Windows = windows.ToList();
        UpdatedAt = now;
    }
}

/// <summary>Vigencia opcional [from, until).</summary>
public static class Validity
{
    public static void Check(DateTimeOffset? from, DateTimeOffset? until)
    {
        if (from is { } f && until is { } u && u <= f)
        {
            throw new DomainValidationException("ValidUntil", "La vigencia debe terminar después de empezar.");
        }
    }
}

/// <summary>
/// Credencial de un sujeto (control RF, tarjeta o PIN). Solo se guarda el HMAC del valor con la clave del sitio
/// (<see cref="CredentialHash"/>); el valor nunca se guarda ni se devuelve.
/// </summary>
public sealed class Credential
{
    private Credential()
    {
        ValueHash = [];
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid SiteId { get; private set; }

    public Guid SubjectId { get; private set; }

    public CredentialType Type { get; private set; }

    public byte[] ValueHash { get; private set; }

    public DateTimeOffset? ValidFrom { get; private set; }

    public DateTimeOffset? ValidUntil { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Credential Create(Guid siteId, Guid subjectId, CredentialType type, byte[] valueHash,
        DateTimeOffset? validFrom, DateTimeOffset? validUntil, DateTimeOffset now)
    {
        Validity.Check(validFrom, validUntil);
        return new Credential
        {
            Id = Guid.CreateVersion7(now),
            SiteId = siteId,
            SubjectId = subjectId,
            Type = type,
            ValueHash = valueHash,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            CreatedAt = now
        };
    }

    public void SetValidity(DateTimeOffset? validFrom, DateTimeOffset? validUntil)
    {
        Validity.Check(validFrom, validUntil);
        ValidFrom = validFrom;
        ValidUntil = validUntil;
    }
}

/// <summary>
/// Otorgamiento (D-13): sujeto → destino (espacio o punto) → horario → vigencia → efecto → origen. Gana siempre la
/// denegación. No se modifica: se elimina y se crea otro.
/// </summary>
public sealed class Grant
{
    private Grant()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid SiteId { get; private set; }

    public Guid SubjectId { get; private set; }

    public GrantTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public Guid? ScheduleId { get; private set; }

    public DateTimeOffset? ValidFrom { get; private set; }

    public DateTimeOffset? ValidUntil { get; private set; }

    public GrantEffect Effect { get; private set; }

    public GrantOrigin Origin { get; private set; }

    public Guid? OriginId { get; private set; }

    public int? RemainingUses { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Grant Create(Guid siteId, Guid subjectId, GrantTargetType targetType, Guid targetId, Guid? scheduleId,
        DateTimeOffset? validFrom, DateTimeOffset? validUntil, GrantEffect effect, int? maxUses, Guid createdBy, DateTimeOffset now)
    {
        Validity.Check(validFrom, validUntil);
        if (maxUses is not null && effect == GrantEffect.Deny)
        {
            throw new DomainValidationException("MaxUses", "Una denegación no tiene límite de usos.");
        }

        if (maxUses is < 1)
        {
            throw new DomainValidationException("MaxUses", "El máximo de usos debe ser al menos 1.");
        }

        return new Grant
        {
            Id = Guid.CreateVersion7(now),
            SiteId = siteId,
            SubjectId = subjectId,
            TargetType = targetType,
            TargetId = targetId,
            ScheduleId = scheduleId,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            Effect = effect,
            Origin = GrantOrigin.Manual,
            RemainingUses = maxUses,
            CreatedBy = createdBy,
            CreatedAt = now
        };
    }

    public bool HasEndedAt(DateTimeOffset now) => ValidUntil is { } until && until <= now;
}

/// <summary>Conversión entre los valores del dominio y sus valores públicos.</summary>
public static class AccessNames
{
    public static bool TryParseCredentialType(string? value, out CredentialType type)
    {
        (var ok, type) = value switch
        {
            CredentialTypeNames.Rf => (true, CredentialType.Rf),
            CredentialTypeNames.Card => (true, CredentialType.Card),
            CredentialTypeNames.Pin => (true, CredentialType.Pin),
            _ => (false, default)
        };
        return ok;
    }

    public static string ToName(CredentialType type) => type switch
    {
        CredentialType.Rf => CredentialTypeNames.Rf,
        CredentialType.Card => CredentialTypeNames.Card,
        CredentialType.Pin => CredentialTypeNames.Pin,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static bool TryParseEffect(string? value, out GrantEffect effect)
    {
        (var ok, effect) = value switch
        {
            GrantEffectNames.Allow => (true, GrantEffect.Allow),
            GrantEffectNames.Deny => (true, GrantEffect.Deny),
            _ => (false, default)
        };
        return ok;
    }

    public static string ToName(GrantEffect effect) => effect == GrantEffect.Allow ? GrantEffectNames.Allow : GrantEffectNames.Deny;

    public static bool TryParseTargetType(string? value, out GrantTargetType type)
    {
        (var ok, type) = value switch
        {
            GrantTargetTypeNames.Space => (true, GrantTargetType.Space),
            GrantTargetTypeNames.AccessPoint => (true, GrantTargetType.AccessPoint),
            _ => (false, default)
        };
        return ok;
    }

    public static string ToName(GrantTargetType type) =>
        type == GrantTargetType.Space ? GrantTargetTypeNames.Space : GrantTargetTypeNames.AccessPoint;

    public static string ToName(GrantOrigin origin) => GrantOriginNames.Manual;
}
