using Nexgate.Common.BuildingBlocks.Telemetry;
using Nexgate.Api.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddNexgateTelemetry("nexgate-workers");
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
