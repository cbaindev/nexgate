using Nexgate.BuildingBlocks.Persistence;
using Npgsql;

namespace Nexgate.Host.Tests;

/// <summary>
/// Guardia de catálogo: toda tabla de negocio debe tener RLS habilitado, forzado y al menos una política.
/// Cubre automáticamente cada módulo nuevo. Excepciones: el padrón (schema people) y el historial de migraciones.
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
}
