using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexgate.BuildingBlocks.Access;

namespace Nexgate.BuildingBlocks.Persistence;

/// <summary>Contexto de un módulo registrado para ejecutar sus migraciones.</summary>
public sealed record ModuleDatabase(Type ContextType, string Schema);

public static class PersistenceExtensions
{
    public const string AppConnectionName = "Default";
    public const string MigrationsConnectionName = "Migrations";

    /// <summary>
    /// Registra el DbContext de un módulo con la conexión de la aplicación (rol sin privilegios) y el interceptor
    /// que fija la organización activa y el usuario actual. Las migraciones se aplican aparte, con el rol owner.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : NexgateDbContext
    {
        services.AddNexgateRequestContext();
        services.AddScoped<SessionContextInterceptor>();
        services.AddDbContext<TContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(AppConnectionName)
                ?? throw new InvalidOperationException($"Falta la cadena de conexión '{AppConnectionName}'.");
            ConfigureNpgsql(options, connectionString, schema);
            options.AddInterceptors(sp.GetRequiredService<SessionContextInterceptor>());
        });
        services.AddSingleton(new ModuleDatabase(typeof(TContext), schema));
        services.AddDatabaseMigrator();
        return services;
    }

    public static IServiceCollection AddDatabaseMigrator(this IServiceCollection services)
    {
        services.AddTransient<DatabaseMigrator>();
        return services;
    }

    public static void ConfigureNpgsql(DbContextOptionsBuilder options, string connectionString, string schema) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(DatabaseRoles.MigrationsHistoryTable, schema));
}
