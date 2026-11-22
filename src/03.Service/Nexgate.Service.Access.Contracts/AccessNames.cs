namespace Nexgate.Service.Access.Contracts;

/// <summary>Valores públicos del tipo de credencial.</summary>
public static class CredentialTypeNames
{
    public const string Rf = "rf";
    public const string Card = "card";
    public const string Pin = "pin";

    public static readonly IReadOnlyList<string> All = [Rf, Card, Pin];
}

/// <summary>Valores públicos del efecto de un otorgamiento. Gana siempre la denegación (D-13).</summary>
public static class GrantEffectNames
{
    public const string Allow = "allow";
    public const string Deny = "deny";

    public static readonly IReadOnlyList<string> All = [Allow, Deny];
}

/// <summary>Valores públicos del tipo de destino de un otorgamiento.</summary>
public static class GrantTargetTypeNames
{
    public const string Space = "space";
    public const string AccessPoint = "access_point";

    public static readonly IReadOnlyList<string> All = [Space, AccessPoint];
}

/// <summary>Valores públicos del origen de un otorgamiento. Los demás orígenes llegan con sus cambios.</summary>
public static class GrantOriginNames
{
    public const string Manual = "manual";
}

/// <summary>Días de la semana de una ventana horaria (el bit de la máscara compilada es el índice: lunes = 0).</summary>
public static class DayNames
{
    public const string Monday = "monday";
    public const string Tuesday = "tuesday";
    public const string Wednesday = "wednesday";
    public const string Thursday = "thursday";
    public const string Friday = "friday";
    public const string Saturday = "saturday";
    public const string Sunday = "sunday";

    public static readonly IReadOnlyList<string> All = [Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday];
}

/// <summary>Resultado de la decisión de acceso.</summary>
public static class AccessDecisionNames
{
    public const string Allow = "allow";
    public const string Deny = "deny";
}

/// <summary>Motivo de la decisión de acceso.</summary>
public static class AccessReasonNames
{
    public const string Allowed = "allowed";
    public const string CredentialInvalid = "credential_invalid";
    public const string ExplicitDeny = "explicit_deny";
    public const string NoPermission = "no_permission";
}

public static class AccessLimits
{
    public const int ScheduleNameMaxLength = 100;
    public const int MaxWindows = 20;
    public const int CredentialValueMaxLength = 64;
}

/// <summary>Códigos de error estables del módulo.</summary>
public static class AccessErrorCodes
{
    public const string ScheduleNameConflict = "schedule_name_conflict";
    public const string ScheduleInUse = "schedule_in_use";
    public const string CredentialConflict = "credential_conflict";
}
