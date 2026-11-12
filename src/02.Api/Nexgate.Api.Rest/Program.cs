using Nexgate.Api.Rest.Extensions;
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
    sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta la cadena de conexión 'Default'.")));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: [HealthEndpoints.ReadyTag]);

builder.Services.AddNexgateControllers();
builder.Services.AddModules();

var app = builder.Build();

app.UseNexgatePipeline();
app.MapNexgateHealth();

// Todos los endpoints de negocio son controllers bajo el prefijo de versión (api/v{version}).
app.MapControllers();

app.Run();

public partial class Program;
