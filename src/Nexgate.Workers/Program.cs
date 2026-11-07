using Nexgate.BuildingBlocks.Telemetry;
using Nexgate.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddNexgateTelemetry("nexgate-workers");
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
