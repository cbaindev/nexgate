using Nexgate.BuildingBlocks.Errors;
using Nexgate.BuildingBlocks.Health;
using Nexgate.BuildingBlocks.Persistence;
using Nexgate.BuildingBlocks.Telemetry;
using Npgsql;
using Nexgate.Modules.Tenancy.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddNexgateTelemetry("nexgate-host");
builder.Services.AddNexgateWebDefaults();

// La cadena de conexión se lee al resolver el servicio (no al arrancar), para que la configuración
// definida por el entorno o por las pruebas siempre se respete.
builder.Services.AddSingleton(sp => NpgsqlDataSource.Create(
    sp.GetRequiredService<IConfiguration>().GetConnectionString(PersistenceExtensions.AppConnectionName)
        ?? throw new InvalidOperationException("Falta la cadena de conexión 'Default'.")));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: [HealthEndpoints.ReadyTag]);

builder.Services.AddDatabaseMigrator();
builder.Services.AddTenancyModule();

var app = builder.Build();

// `dotnet run --project src/Nexgate.Host -- migrate`: aplica las migraciones con el rol owner
// (ConnectionStrings:Migrations) y termina. La API nunca migra al arrancar.
if (args.Contains("migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync();
    return;
}

app.UseNexgatePipeline();
app.MapNexgateHealth();

// Todos los endpoints de negocio viven bajo el prefijo de versión.
var api = app.MapGroup("/api/v1");
api.MapTenancyEndpoints();

app.Run();

public partial class Program;
