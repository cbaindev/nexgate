using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexgate.BuildingBlocks.Persistence;
using Npgsql;

namespace Nexgate.Host.Tests;

/// <summary>
/// Base de datos de pruebas, nueva en cada corrida: se crea y se migra con el rol owner, y la API se conecta con
/// el rol de la aplicación (sin privilegios), igual que en producción.
/// Cadenas: NEXGATE_TEST_PG_OWNER y NEXGATE_TEST_PG, o las de src/Nexgate.Host/appsettings.json (docker-compose).
/// </summary>
public sealed class TestDatabase
{
    private static readonly Lazy<Task<TestDatabase>> Shared = new(CreateAsync);

    private TestDatabase(string name, string ownerConnectionString, string appConnectionString)
    {
        Name = name;
        OwnerConnectionString = ownerConnectionString;
        AppConnectionString = appConnectionString;
    }

    public string Name { get; }

    public string OwnerConnectionString { get; }

    public string AppConnectionString { get; }

    public static Task<TestDatabase> GetAsync() => Shared.Value;

    /// <summary>Conexión como rol de la aplicación con un tamaño de pool propio (para pruebas de reutilización).</summary>
    public string AppConnection(int? maxPoolSize = null) =>
        maxPoolSize is { } size
            ? new NpgsqlConnectionStringBuilder(AppConnectionString) { MaxPoolSize = size, ApplicationName = $"pool-{Guid.NewGuid():N}" }.ConnectionString
            : AppConnectionString;

    private static async Task<TestDatabase> CreateAsync()
    {
        var settings = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
            .Build();

        var owner = Environment.GetEnvironmentVariable("NEXGATE_TEST_PG_OWNER")
            ?? settings.GetConnectionString(PersistenceExtensions.MigrationsConnectionName)
            ?? throw new InvalidOperationException("Falta la cadena del rol owner para las pruebas.");
        var app = Environment.GetEnvironmentVariable("NEXGATE_TEST_PG")
            ?? settings.GetConnectionString(PersistenceExtensions.AppConnectionName)
            ?? throw new InvalidOperationException("Falta la cadena del rol de la aplicación para las pruebas.");

        var name = $"nexgate_test_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..48];
        await using (var dataSource = NpgsqlDataSource.Create(owner))
        await using (var create = dataSource.CreateCommand($"CREATE DATABASE {name}"))
        {
            await create.ExecuteNonQueryAsync();
        }

        var database = new TestDatabase(
            name,
            new NpgsqlConnectionStringBuilder(owner) { Database = name }.ConnectionString,
            new NpgsqlConnectionStringBuilder(app) { Database = name }.ConnectionString);

        // Las migraciones se aplican con la misma composición que usa el comando `migrate` del host.
        await using (var factory = new NexgateFactory(database))
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync();
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => database.Drop(owner);
        return database;
    }

    private void Drop(string owner)
    {
        try
        {
            NpgsqlConnection.ClearAllPools();
            using var dataSource = NpgsqlDataSource.Create(owner);
            using var drop = dataSource.CreateCommand($"DROP DATABASE IF EXISTS {Name} WITH (FORCE)");
            drop.ExecuteNonQuery();
        }
        catch (NpgsqlException)
        {
            // Mejor esfuerzo: una base de pruebas huérfana no afecta a la siguiente corrida.
        }
    }
}
