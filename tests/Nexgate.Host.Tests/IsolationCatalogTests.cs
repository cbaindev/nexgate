using Nexgate.BuildingBlocks.Persistence;
using Nexgate.Modules.Identity.Infrastructure;
using Npgsql;

namespace Nexgate.Host.Tests;

/// <summary>
/// Guardia de catálogo: toda tabla de negocio debe tener RLS habilitado, forzado y al menos una política.
/// Cubre automáticamente cada módulo nuevo. Excepciones: el padrón (schema people) y el historial de migraciones.
/// Además, toda función <c>SECURITY DEFINER</c> tiene que estar en la lista conocida y fijar <c>search_path</c>.
/// </summary>
public class IsolationCatalogTests
{
    private const string UnprotectedTablesSql = $"""
        SELECT n.nspname || '.' || c.relname
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind IN ('r', 'p')
          AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'people')
          AND n.nspname NOT LIKE 'pg_toast%'
          AND n.nspname NOT LIKE 'pg_temp%'
          AND c.relname <> '{DatabaseRoles.MigrationsHistoryTable}'
          AND (NOT c.relrowsecurity
               OR NOT c.relforcerowsecurity
               OR NOT EXISTS (SELECT 1 FROM pg_policy p WHERE p.polrelid = c.oid))
        ORDER BY 1
        """;

    private static async Task<List<string>> UnprotectedTablesAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(UnprotectedTablesSql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        var tables = new List<string>();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    [Fact]
    public async Task Every_business_table_has_row_level_security_enabled_forced_and_with_a_policy()
    {
        var db = await TestDatabase.GetAsync();
        await using var connection = new NpgsqlConnection(db.OwnerConnectionString);
        await connection.OpenAsync();

        var unprotected = await UnprotectedTablesAsync(connection);

        Assert.True(unprotected.Count == 0,
            "Tablas sin aislamiento (usar EnableTenantIsolation en la migración): " + string.Join(", ", unprotected));
    }

    [Fact]
    public async Task The_guard_detects_a_table_without_isolation()
    {
        var db = await TestDatabase.GetAsync();
        await using var connection = new NpgsqlConnection(db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var create = new NpgsqlCommand(
            "CREATE SCHEMA guard_probe; " +
            "CREATE TABLE guard_probe.without_rls (id uuid, organization_id uuid); " +
            "CREATE TABLE guard_probe.not_forced (id uuid, organization_id uuid); " +
            "ALTER TABLE guard_probe.not_forced ENABLE ROW LEVEL SECURITY; " +
            "CREATE POLICY p ON guard_probe.not_forced USING (true);", connection, transaction))
        {
            await create.ExecuteNonQueryAsync();
        }

        var unprotected = await UnprotectedTablesAsync(connection, transaction);
        await transaction.RollbackAsync();

        Assert.Contains("guard_probe.without_rls", unprotected);
        Assert.Contains("guard_probe.not_forced", unprotected);
    }

    private const string SecurityDefinerFunctionsSql = """
        SELECT n.nspname || '.' || p.proname,
               EXISTS (SELECT 1 FROM unnest(coalesce(p.proconfig, '{}')) c WHERE c LIKE 'search_path=%')
        FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE p.prosecdef
          AND n.nspname NOT IN ('pg_catalog', 'information_schema')
        ORDER BY 1
        """;

    private static readonly IReadOnlyList<string> KnownSecurityDefinerFunctions = IdentityIsolationSql.SecurityDefinerFunctions;

    private static async Task<List<string>> UnsafeSecurityDefinerFunctionsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(SecurityDefinerFunctionsSql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        var unsafeFunctions = new List<string>();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            if (!KnownSecurityDefinerFunctions.Contains(name) || !reader.GetBoolean(1))
            {
                unsafeFunctions.Add(name);
            }
        }

        return unsafeFunctions;
    }

    [Fact]
    public async Task Every_security_definer_function_is_known_and_fixes_its_search_path()
    {
        var db = await TestDatabase.GetAsync();
        await using var connection = new NpgsqlConnection(db.OwnerConnectionString);
        await connection.OpenAsync();

        var unsafeFunctions = await UnsafeSecurityDefinerFunctionsAsync(connection);

        Assert.True(unsafeFunctions.Count == 0,
            "Funciones SECURITY DEFINER desconocidas o sin search_path: " + string.Join(", ", unsafeFunctions));
    }

    [Fact]
    public async Task The_guard_detects_an_unknown_security_definer_function_or_one_without_search_path()
    {
        var db = await TestDatabase.GetAsync();
        await using var connection = new NpgsqlConnection(db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var create = new NpgsqlCommand(
            "CREATE SCHEMA definer_probe; " +
            "CREATE FUNCTION definer_probe.no_search_path() RETURNS int LANGUAGE sql SECURITY DEFINER AS 'SELECT 1'; " +
            "CREATE FUNCTION definer_probe.unknown() RETURNS int LANGUAGE sql SECURITY DEFINER SET search_path = pg_catalog AS 'SELECT 1';",
            connection, transaction))
        {
            await create.ExecuteNonQueryAsync();
        }

        var unsafeFunctions = await UnsafeSecurityDefinerFunctionsAsync(connection, transaction);
        await transaction.RollbackAsync();

        Assert.Contains("definer_probe.no_search_path", unsafeFunctions);
        Assert.Contains("definer_probe.unknown", unsafeFunctions);
    }
}
