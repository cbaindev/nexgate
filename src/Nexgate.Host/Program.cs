using Nexgate.BuildingBlocks.Errors;
using Nexgate.BuildingBlocks.Health;
using Nexgate.BuildingBlocks.Telemetry;
using Npgsql;
using Nexgate.Modules.Sample.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddNexgateTelemetry("nexgate-host");
builder.Services.AddNexgateWebDefaults();

// La cadena de conexión se lee al resolver el servicio (no al arrancar), para que la configuración
// definida por el entorno o por las pruebas siempre se respete.
builder.Services.AddSingleton(sp => NpgsqlDataSource.Create(
    sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta la cadena de conexión 'Default'.")));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: [HealthEndpoints.ReadyTag]);

builder.Services.AddSampleModule();

var app = builder.Build();

app.UseNexgatePipeline();
app.MapNexgateHealth();

// Todos los endpoints de negocio viven bajo el prefijo de versión.
var api = app.MapGroup("/api/v1");
api.MapSampleEndpoints(app.Configuration);

app.Run();

public partial class Program;
