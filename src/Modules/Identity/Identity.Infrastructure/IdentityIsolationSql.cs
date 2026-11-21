using Nexgate.BuildingBlocks.Persistence;

namespace Nexgate.Modules.Identity.Infrastructure;

/// <summary>
/// Políticas RLS y funciones restringidas del schema <c>identity</c> (design, decisiones 5 y 6). Las usa la migración
/// inicial; las pruebas las verifican contra la base real.
/// </summary>
public static class IdentityIsolationSql
{
    /// <summary>Funciones <c>SECURITY DEFINER</c> conocidas. La guardia de catálogo rechaza cualquier otra.</summary>
    public static readonly IReadOnlyList<string> SecurityDefinerFunctions =
        ["identity.find_login", "identity.find_refresh_token", "identity.find_invitation"];

    private const string Owner = DatabaseRoles.Owner;
    private const string App = DatabaseRoles.App;

    public static IReadOnlyList<string> Up =>
    [
        // user_account: dato de identidad de la plataforma, aislado por usuario. Cada usuario ve su cuenta y las de los
        // miembros de su organización activa; solo crea y modifica la propia.
        "ALTER TABLE identity.user_account ENABLE ROW LEVEL SECURITY;",
        "ALTER TABLE identity.user_account FORCE ROW LEVEL SECURITY;",
        """
        CREATE POLICY user_account_select ON identity.user_account FOR SELECT
            USING (id = public.nexgate_current_user()
                   OR EXISTS (SELECT 1 FROM identity.membership m
                              WHERE m.user_id = user_account.id AND m.organization_id = public.nexgate_current_org()));
        """,
        "CREATE POLICY user_account_insert ON identity.user_account FOR INSERT WITH CHECK (id = public.nexgate_current_user());",
        "CREATE POLICY user_account_update ON identity.user_account FOR UPDATE " +
        "USING (id = public.nexgate_current_user()) WITH CHECK (id = public.nexgate_current_user());",

        // membership: política estándar por organización, más la lectura de las membresías propias (selector).
        .. TenantIsolationMigrationExtensions.TenantIsolationSql("identity", "membership"),
        "CREATE POLICY membership_own ON identity.membership FOR SELECT USING (user_id = public.nexgate_current_user());",

        .. TenantIsolationMigrationExtensions.TenantIsolationSql("identity", "role_assignment"),
        .. TenantIsolationMigrationExtensions.TenantIsolationSql("identity", "invitation"),

        // Sesiones y tokens de renovación: del usuario.
        "ALTER TABLE identity.refresh_session ENABLE ROW LEVEL SECURITY;",
        "ALTER TABLE identity.refresh_session FORCE ROW LEVEL SECURITY;",
        "CREATE POLICY user_isolation ON identity.refresh_session " +
        "USING (user_id = public.nexgate_current_user()) WITH CHECK (user_id = public.nexgate_current_user());",
        "ALTER TABLE identity.refresh_token ENABLE ROW LEVEL SECURITY;",
        "ALTER TABLE identity.refresh_token FORCE ROW LEVEL SECURITY;",
        "CREATE POLICY user_isolation ON identity.refresh_token " +
        "USING (user_id = public.nexgate_current_user()) WITH CHECK (user_id = public.nexgate_current_user());",

        // Con FORCE, RLS también aplica al dueño. Las funciones restringidas corren como dueño y necesitan leer;
        // el dueño ya puede todo sobre sus tablas, así que esto no le da a nadie un acceso nuevo.
        $"CREATE POLICY owner_lookup ON identity.user_account FOR SELECT TO {Owner} USING (true);",
        $"CREATE POLICY owner_lookup ON identity.invitation FOR SELECT TO {Owner} USING (true);",
        $"CREATE POLICY owner_lookup ON identity.refresh_session FOR SELECT TO {Owner} USING (true);",
        $"CREATE POLICY owner_lookup ON identity.refresh_token FOR SELECT TO {Owner} USING (true);",

        // Búsquedas previas a la autenticación: una fila por clave exacta, nunca un listado.
        """
        CREATE FUNCTION identity.find_login(p_normalized_email text)
            RETURNS TABLE (user_id uuid, password_hash text, status text, platform_operator boolean)
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, pg_temp
            AS $$
                SELECT u.id, u.password_hash, u.status, u.platform_operator
                FROM identity.user_account u
                WHERE u.normalized_email = p_normalized_email
            $$;
        """,
        """
        CREATE FUNCTION identity.find_refresh_token(p_token_hash bytea)
            RETURNS TABLE (token_id uuid, session_id uuid, user_id uuid, used_at timestamptz,
                           session_revoked_at timestamptz, session_expires_at timestamptz, active_organization_id uuid)
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, pg_temp
            AS $$
                SELECT t.id, t.session_id, t.user_id, t.used_at, s.revoked_at, s.expires_at, s.active_organization_id
                FROM identity.refresh_token t
                JOIN identity.refresh_session s ON s.id = t.session_id
                WHERE t.token_hash = p_token_hash
            $$;
        """,
        """
        CREATE FUNCTION identity.find_invitation(p_token_hash bytea)
            RETURNS TABLE (invitation_id uuid, organization_id uuid, normalized_email text)
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, pg_temp
            AS $$
                SELECT i.id, i.organization_id, i.normalized_email
                FROM identity.invitation i
                WHERE i.token_hash = p_token_hash AND i.accepted_at IS NULL AND i.expires_at > now()
            $$;
        """,
        "REVOKE ALL ON FUNCTION identity.find_login(text) FROM PUBLIC;",
        "REVOKE ALL ON FUNCTION identity.find_refresh_token(bytea) FROM PUBLIC;",
        "REVOKE ALL ON FUNCTION identity.find_invitation(bytea) FROM PUBLIC;",
        $"GRANT EXECUTE ON FUNCTION identity.find_login(text) TO {App};",
        $"GRANT EXECUTE ON FUNCTION identity.find_refresh_token(bytea) TO {App};",
        $"GRANT EXECUTE ON FUNCTION identity.find_invitation(bytea) TO {App};"
    ];

    public static IReadOnlyList<string> Down =>
    [
        "DROP FUNCTION IF EXISTS identity.find_invitation(bytea);",
        "DROP FUNCTION IF EXISTS identity.find_refresh_token(bytea);",
        "DROP FUNCTION IF EXISTS identity.find_login(text);"
    ];
}
