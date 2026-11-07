namespace Nexgate.BuildingBlocks.Persistence;

/// <summary>
/// Objetos compartidos por todos los schemas. Se aplican antes de las migraciones de los módulos y son idempotentes.
/// </summary>
public static class DatabaseBootstrap
{
    /// <summary>
    /// <c>nexgate_current_org()</c>: la organización activa o NULL. El NULLIF es necesario porque, una vez fijada
    /// en una conexión, la variable queda como cadena vacía y <c>''::uuid</c> fallaría. Con NULL ninguna fila
    /// cumple la política: sin organización activa no se ve nada (falla cerrado).
    /// </summary>
    public const string Sql = $"""
        CREATE OR REPLACE FUNCTION public.nexgate_current_org() RETURNS uuid
            LANGUAGE sql STABLE PARALLEL SAFE
            AS $$ SELECT NULLIF(current_setting('{DatabaseRoles.OrganizationSetting}', true), '')::uuid $$;
        REVOKE ALL ON FUNCTION public.nexgate_current_org() FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION public.nexgate_current_org() TO {DatabaseRoles.App};
        """;
}
