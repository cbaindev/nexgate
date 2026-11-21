using Microsoft.EntityFrameworkCore.Migrations;

namespace Nexgate.BuildingBlocks.Persistence;

/// <summary>
/// Sentencias estándar de aislamiento para las migraciones de los módulos. Todas las tablas de negocio usan
/// la misma política, así la prueba de catálogo y la revisión de código tienen una sola forma que verificar.
/// </summary>
public static class TenantIsolationMigrationExtensions
{
    public const string PolicyName = "tenant_isolation";

    /// <summary>RLS habilitado y forzado, con la política estándar por <c>organization_id</c>.</summary>
    public static MigrationBuilder EnableTenantIsolation(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        foreach (var statement in TenantIsolationSql(schema, table))
        {
            migrationBuilder.Sql(statement);
        }

        return migrationBuilder;
    }

    /// <summary>
    /// Permisos del rol de la aplicación sobre el schema: uso, DML sobre tablas y secuencias (actuales y futuras),
    /// y ningún acceso al historial de migraciones.
    /// </summary>
    public static MigrationBuilder GrantAppAccess(this MigrationBuilder migrationBuilder, string schema)
    {
        foreach (var statement in GrantAppAccessSql(schema))
        {
            migrationBuilder.Sql(statement);
        }

        return migrationBuilder;
    }

    public static IReadOnlyList<string> TenantIsolationSql(string schema, string table)
    {
        var name = $"{Quote(schema)}.{Quote(table)}";
        return
        [
            $"ALTER TABLE {name} ENABLE ROW LEVEL SECURITY;",
            $"ALTER TABLE {name} FORCE ROW LEVEL SECURITY;",
            $"CREATE POLICY {PolicyName} ON {name} " +
            "USING (organization_id = public.nexgate_current_org()) " +
            "WITH CHECK (organization_id = public.nexgate_current_org());"
        ];
    }

    public static IReadOnlyList<string> GrantAppAccessSql(string schema)
    {
        var s = Quote(schema);
        var app = DatabaseRoles.App;
        return
        [
            $"GRANT USAGE ON SCHEMA {s} TO {app};",
            $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {s} TO {app};",
            $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA {s} TO {app};",
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA {s} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {app};",
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA {s} GRANT USAGE, SELECT ON SEQUENCES TO {app};",
            $"REVOKE ALL ON {s}.{Quote(DatabaseRoles.MigrationsHistoryTable)} FROM {app};"
        ];
    }

    private static string Quote(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !identifier.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_'))
        {
            throw new ArgumentException($"Identificador SQL inválido: '{identifier}'. Usar minúsculas, dígitos y guiones bajos.", nameof(identifier));
        }

        return "\"" + identifier + "\"";
    }
}
