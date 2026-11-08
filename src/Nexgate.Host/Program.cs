using Nexgate.BuildingBlocks.Errors;
using Nexgate.BuildingBlocks.Health;
using Nexgate.BuildingBlocks.OpenApi;
using Nexgate.BuildingBlocks.Persistence;
using Nexgate.BuildingBlocks.Telemetry;
using Nexgate.Host;
using Nexgate.Modules.Identity.Infrastructure;
using Nexgate.Modules.Tenancy.Infrastructure;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddNexgateTelemetry("nexgate-host");
builder.Services.AddNexgateWebDefaults();
builder.Services.AddNexgateOpenApi();

// La cadena de conexión se lee al resolver el servicio (no al arrancar), para que la configuración
// definida por el entorno o por las pruebas siempre se respete.
builder.Services.AddSingleton(sp => NpgsqlDataSource.Create(
    sp.GetRequiredService<IConfiguration>().GetConnectionString(PersistenceExtensions.AppConnectionName)
        ?? throw new InvalidOperationException("Falta la cadena de conexión 'Default'.")));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: [HealthEndpoints.ReadyTag]);

builder.Services.AddDatabaseMigrator();
builder.Services.AddTenancyModule();
builder.Services.AddIdentityModule();

var app = builder.Build();

// `dotnet run --project src/Nexgate.Host -- migrate`: aplica las migraciones con el rol owner
// (ConnectionStrings:Migrations) y termina. La API nunca migra al arrancar.
if (args.Contains("migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync();
    return;
}

// `dotnet run --project src/Nexgate.Host -- create-platform-operator --email <correo>`: crea el operador de
// plataforma (o le da la marca a una cuenta existente). La contraseña se lee de NEXGATE_OPERATOR_PASSWORD o de la
// entrada estándar, nunca de los argumentos.
if (args.Contains(AdminCommands.CreatePlatformOperator))
{
    Environment.ExitCode = await AdminCommands.CreatePlatformOperatorAsync(app.Services, args, Console.In, Console.Out);
    return;
}

app.UseNexgatePipeline();
app.MapNexgateHealth();
app.MapNexgateApiReference();

// Todos los endpoints de negocio viven bajo el prefijo de versión. Cualquiera puede responder 500 (error inesperado).
var api = app.MapGroup("/api/v1").Problems(StatusCodes.Status500InternalServerError);
api.MapIdentityEndpoints();
api.MapTenancyEndpoints();

app.Run();

public partial class Program;
