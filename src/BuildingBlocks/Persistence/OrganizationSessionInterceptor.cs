using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nexgate.BuildingBlocks.Tenancy;

namespace Nexgate.BuildingBlocks.Persistence;

/// <summary>
/// Fija la organización activa en la sesión de PostgreSQL cada vez que EF abre una conexión. Sin organización activa
/// la deja vacía explícitamente, así una conexión reutilizada nunca conserva la de una solicitud anterior
/// (además, Npgsql reinicia el estado de la sesión al devolver la conexión al pool).
/// </summary>
public sealed class OrganizationSessionInterceptor(ICurrentOrganization currentOrganization) : DbConnectionInterceptor
{
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
        command.CommandText = $"SELECT set_config('{DatabaseRoles.OrganizationSetting}', @organization, false)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "organization";
        parameter.Value = currentOrganization.Id?.ToString() ?? string.Empty;
        command.Parameters.Add(parameter);
        return command;
    }
}
