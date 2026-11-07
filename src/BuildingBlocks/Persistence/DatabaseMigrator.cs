using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexgate.BuildingBlocks.Tenancy;
using Npgsql;

namespace Nexgate.BuildingBlocks.Persistence;

/// <summary>
/// Aplica los objetos compartidos y las migraciones de todos los módulos con el rol owner
/// (<c>ConnectionStrings:Migrations</c>). La aplicación en ejecución nunca migra: su rol no tiene permisos.
/// </summary>
public sealed class DatabaseMigrator(
    IServiceProvider services,
    IConfiguration configuration,
    IEnumerable<ModuleDatabase> modules,
    ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString(PersistenceExtensions.MigrationsConnectionName)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión '{PersistenceExtensions.MigrationsConnectionName}' (rol owner).");

        await using (var dataSource = NpgsqlDataSource.Create(connectionString))
        await using (var command = dataSource.CreateCommand(DatabaseBootstrap.Sql))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var module in modules.DistinctBy(m => m.ContextType))
        {
            var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(
                typeof(DbContextOptionsBuilder<>).MakeGenericType(module.ContextType))!;
            PersistenceExtensions.ConfigureNpgsql(optionsBuilder, connectionString, module.Schema);

            await using var context = (DbContext)ActivatorUtilities.CreateInstance(
                services, module.ContextType, optionsBuilder.Options, new NullCurrentOrganization());

            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            logger.LogInformation("Schema {Schema}: {Count} migraciones pendientes", module.Schema, pending);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
