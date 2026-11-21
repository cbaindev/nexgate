namespace Nexgate.BuildingBlocks.Persistence;

/// <summary>Roles y variables de sesión de PostgreSQL (los roles los crea infra/postgres/init/01-roles.sh).</summary>
public static class DatabaseRoles
{
    /// <summary>Rol de la aplicación en ejecución: no es dueño de nada y no puede saltear RLS.</summary>
    public const string App = "nexgate_app";

    /// <summary>Rol dueño de schemas, tablas y funciones; ejecuta las migraciones.</summary>
    public const string Owner = "nexgate_owner";

    /// <summary>Variable de sesión con la organización activa.</summary>
    public const string OrganizationSetting = "app.org_id";

    /// <summary>Variable de sesión con el usuario actual.</summary>
    public const string UserSetting = "app.user_id";

    /// <summary>Variable local a la transacción que habilita operaciones de plataforma (alta de organizaciones).</summary>
    public const string PlatformOperationSetting = "app.platform_operation";

    /// <summary>Tabla de historial de migraciones de cada schema (sin acceso para el rol de la aplicación).</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";
}
