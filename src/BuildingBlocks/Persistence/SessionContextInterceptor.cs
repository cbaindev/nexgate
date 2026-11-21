using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nexgate.BuildingBlocks.Access;
using Nexgate.BuildingBlocks.Tenancy;

namespace Nexgate.BuildingBlocks.Persistence;

/// <summary>
/// Fija la organización activa y el usuario actual en la sesión de PostgreSQL cada vez que EF abre una conexión.
/// Sin valor los deja vacíos explícitamente, así una conexión reutilizada nunca conserva los de una solicitud anterior
/// (además, Npgsql reinicia el estado de la sesión al devolver la conexión al pool).
/// </summary>
public sealed class SessionContextInterceptor(ICurrentOrganization currentOrganization, ICurrentUser currentUser) : DbConnectionInterceptor
{
    public const string Sql =
        $"SELECT set_config('{DatabaseRoles.OrganizationSetting}', @organization, false), " +
        $"set_config('{DatabaseRoles.UserSetting}', @user, false)";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = Sql;
        AddParameter(command, "organization", currentOrganization.Id);
        AddParameter(command, "user", currentUser.Id);
        return command;
    }

    private static void AddParameter(DbCommand command, string name, Guid? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value?.ToString() ?? string.Empty;
        command.Parameters.Add(parameter);
    }
}
