using Nexgate.Api.Rest.Commands;
using Nexgate.Api.Rest.Extensions;
using Nexgate.Api.Rest.Extensions.OpenApi;
using Nexgate.Common.BuildingBlocks.Persistence;
using Nexgate.Common.BuildingBlocks.Health;
using Nexgate.Common.BuildingBlocks.Telemetry;
using Npgsql;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.AddNexgateTelemetry("nexgate-host", tracing => tracing.AddAspNetCoreInstrumentation());
builder.Services.AddNexgateWebDefaults();

// La cadena de conexión se lee al resolver el servicio (no al arrancar), para que la configuración
// definida por el entorno o por las pruebas siempre se respete.
builder.Services.AddSingleton(sp => NpgsqlDataSource.Create(
    sp.GetRequiredService<IConfiguration>().GetConnectionString(PersistenceExtensions.AppConnectionName)
        ?? throw new InvalidOperationException("Falta la cadena de conexión 'Default'.")));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: [HealthEndpoints.ReadyTag]);

builder.Services.AddNexgateControllers();
builder.Services.AddNexgateOpenApi();
builder.Services.AddModules();

var app = builder.Build();

// `dotnet run --project src/02.Api/Nexgate.Api.Rest -- migrate`: aplica las migraciones con el rol owner
// (ConnectionStrings:Migrations) y termina. La API nunca migra al arrancar.
if (args.Contains("migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync();
    return;
}

// `dotnet run --project src/02.Api/Nexgate.Api.Rest -- create-platform-operator --email <correo>`: crea el operador de
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

// Todos los endpoints de negocio son controllers bajo el prefijo de versión (api/v{version}).
app.MapControllers();

app.Run();

public partial class Program;
