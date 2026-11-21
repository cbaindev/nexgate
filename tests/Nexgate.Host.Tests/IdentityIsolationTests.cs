using System.Security.Cryptography;
using Nexgate.Modules.Identity.Infrastructure;
using Npgsql;

namespace Nexgate.Host.Tests;

/// <summary>
/// Aislamiento de los datos de identidad garantizado por la base: SQL directo como rol de la aplicación, sin los filtros
/// de EF (tenant-isolation, <c>Isolation of platform identity data</c>).
/// </summary>
public class IdentityIsolationTests
{
    private static async Task<List<Guid>> GuidsAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<Guid>();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetGuid(0));
        }

        return result;
    }

    private sealed record Scenario(Guid OrganizationA, Guid OrganizationB, TestUser AdminA, TestUser Shared, TestUser AdminB);

    /// <summary>Dos organizaciones; un usuario es miembro de ambas.</summary>
    private static async Task<Scenario> ScenarioAsync(NexgateFactory factory)
    {
        var (shared, a, b) = await AuthenticationTests.UserInTwoOrganizationsAsync(factory);
        return new Scenario(a, b, factory.Administrator(a), shared, factory.Administrator(b));
    }

    [Fact]
    public async Task User_without_an_active_organization_sees_only_its_own_account()
    {
        using var factory = new NexgateFactory();
        var s = await ScenarioAsync(factory);
        var db = await TestDatabase.GetAsync();

        await using var connection = await db.OpenAsAppAsync(organization: null, user: s.Shared.UserId);

        Assert.Equal([s.Shared.UserId], await GuidsAsync(connection, "SELECT id FROM identity.user_account"));
    }

    [Fact]
    public async Task Member_of_an_organization_sees_only_the_accounts_of_its_members()
    {
        using var factory = new NexgateFactory();
        var s = await ScenarioAsync(factory);
        var db = await TestDatabase.GetAsync();

        await using var connection = await db.OpenAsAppAsync(s.OrganizationA, s.AdminA.UserId);
        var visible = await GuidsAsync(connection, "SELECT id FROM identity.user_account");

        Assert.Equal(new[] { s.AdminA.UserId, s.Shared.UserId }.Order(), visible.Order());
        Assert.DoesNotContain(s.AdminB.UserId, visible);
    }

    [Fact]
    public async Task Own_memberships_in_other_organizations_are_visible_but_nobody_elses()
    {
        using var factory = new NexgateFactory();
        var s = await ScenarioAsync(factory);
        var db = await TestDatabase.GetAsync();

        await using (var asShared = await db.OpenAsAppAsync(organization: null, user: s.Shared.UserId))
        {
            Assert.Equal(new[] { s.OrganizationA, s.OrganizationB }.Order(),
                (await GuidsAsync(asShared, "SELECT organization_id FROM identity.membership")).Order());
        }

        // El administrador de A ve los miembros de A, pero no la membresía del usuario compartido en B.
        await using var asAdminA = await db.OpenAsAppAsync(s.OrganizationA, s.AdminA.UserId);
        Assert.Equal([s.OrganizationA], (await GuidsAsync(asAdminA, "SELECT organization_id FROM identity.membership")).Distinct());
        Assert.Equal([s.OrganizationA], (await GuidsAsync(asAdminA, "SELECT organization_id FROM identity.role_assignment")).Distinct());
    }

    [Fact]
    public async Task Sessions_of_other_users_are_never_visible()
    {
        using var factory = new NexgateFactory();
        var s = await ScenarioAsync(factory);
        var db = await TestDatabase.GetAsync();

        await using var connection = await db.OpenAsAppAsync(s.OrganizationA, s.AdminA.UserId);

        Assert.All(await GuidsAsync(connection, "SELECT user_id FROM identity.refresh_session"), id => Assert.Equal(s.AdminA.UserId, id));
        Assert.All(await GuidsAsync(connection, "SELECT user_id FROM identity.refresh_token"), id => Assert.Equal(s.AdminA.UserId, id));
        Assert.NotEmpty(await GuidsAsync(connection, "SELECT id FROM identity.refresh_session"));
    }

    [Theory]
    [InlineData("SELECT id FROM identity.user_account")]
    [InlineData("SELECT id FROM identity.membership")]
    [InlineData("SELECT id FROM identity.role_assignment")]
    [InlineData("SELECT id FROM identity.invitation")]
    [InlineData("SELECT id FROM identity.refresh_session")]
    [InlineData("SELECT id FROM identity.refresh_token")]
    public async Task Without_user_or_organization_identity_tables_return_no_rows(string sql)
    {
        using var factory = new NexgateFactory();
        await ScenarioAsync(factory);
        var db = await TestDatabase.GetAsync();

        await using var connection = await db.OpenAsAppAsync();

        Assert.Empty(await GuidsAsync(connection, sql));
    }

    [Fact]
    public async Task Writing_another_users_account_or_session_is_rejected_or_has_no_effect()
    {
        using var factory = new NexgateFactory();
        var s = await ScenarioAsync(factory);
        var db = await TestDatabase.GetAsync();
        await using var connection = await db.OpenAsAppAsync(s.OrganizationA, s.AdminA.UserId);

        await using (var update = new NpgsqlCommand("UPDATE identity.user_account SET platform_operator = true WHERE id = @id", connection))
        {
            update.Parameters.AddWithValue("id", s.Shared.UserId);
            Assert.Equal(0, await update.ExecuteNonQueryAsync());
        }

        await using (var revoke = new NpgsqlCommand("UPDATE identity.refresh_session SET revoked_at = now() WHERE user_id = @id", connection))
        {
            revoke.Parameters.AddWithValue("id", s.AdminB.UserId);
            Assert.Equal(0, await revoke.ExecuteNonQueryAsync());
        }

        await using var insert = new NpgsqlCommand(
            "INSERT INTO identity.user_account (id, email, normalized_email, password_hash, status, platform_operator, created_at) " +
            "VALUES (@id, 'x@x.test', 'x@x.test', 'h', 'active', true, now())", connection);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        var error = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Theory]
    [InlineData("SELECT * FROM identity.find_login('%')")]
    [InlineData("SELECT * FROM identity.find_login('')")]
    [InlineData("SELECT * FROM identity.find_login(NULL)")]
    [InlineData("SELECT * FROM identity.find_invitation(''::bytea)")]
    [InlineData("SELECT * FROM identity.find_refresh_token(NULL)")]
    public async Task Restricted_lookups_never_list(string sql)
    {
        using var factory = new NexgateFactory();
        await ScenarioAsync(factory);
        var db = await TestDatabase.GetAsync();
        await using var connection = await db.OpenAsAppAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task Restricted_lookup_returns_only_the_requested_account()
    {
        using var factory = new NexgateFactory();
        var s = await ScenarioAsync(factory);
        var db = await TestDatabase.GetAsync();
        await using var connection = await db.OpenAsAppAsync();

        await using var command = new NpgsqlCommand("SELECT user_id FROM identity.find_login(@email)", connection);
        command.Parameters.AddWithValue("email", s.AdminB.Email.ToLowerInvariant());

        Assert.Equal(s.AdminB.UserId, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Restricted_lookups_can_be_executed_only_by_the_application_and_owner_roles()
    {
        var db = await TestDatabase.GetAsync();
        await using var connection = new NpgsqlConnection(db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT n.nspname || '.' || p.proname,
                   has_function_privilege('nexgate_app', p.oid, 'EXECUTE'),
                   coalesce(array_to_string(p.proacl, ','), '')
            FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE p.prosecdef AND n.nspname = 'identity'
            ORDER BY 1
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var functions = new List<string>();
        while (await reader.ReadAsync())
        {
            functions.Add(reader.GetString(0));
            Assert.True(reader.GetBoolean(1));
            // Una entrada "=X/..." sería EXECUTE para PUBLIC.
            Assert.DoesNotContain(reader.GetString(2).Split(','), acl => acl.StartsWith('='));
        }

        Assert.Equal(IdentityIsolationSql.SecurityDefinerFunctions.Order(), functions);
    }

    [Fact]
    public async Task Refresh_and_invitation_tokens_are_stored_only_as_hashes()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        var admin = factory.Administrator(organization);
        var invitation = await factory.InviteAsync(organization, NexgateFactory.NewEmail(), "guard");
        var db = await TestDatabase.GetAsync();

        await using var connection = await db.OpenAsAppAsync(organization, admin.UserId);
        await using var tokens = new NpgsqlCommand(
            "SELECT count(*) FROM identity.refresh_token WHERE token_hash = @hash", connection);
        tokens.Parameters.AddWithValue("hash", SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(admin.Tokens.RefreshToken)));
        await using var invitations = new NpgsqlCommand(
            "SELECT count(*) FROM identity.invitation WHERE token_hash = @hash", connection);
        invitations.Parameters.AddWithValue("hash", SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(invitation.Token)));
        await using var plain = new NpgsqlCommand(
            "SELECT count(*) FROM identity.invitation WHERE token_hash = convert_to(@token, 'UTF8')", connection);
        plain.Parameters.AddWithValue("token", invitation.Token);

        Assert.Equal(1L, await tokens.ExecuteScalarAsync());
        Assert.Equal(1L, await invitations.ExecuteScalarAsync());
        Assert.Equal(0L, await plain.ExecuteScalarAsync());
    }
}
