using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Nexgate.Common.BuildingBlocks.Telemetry;

public static class TelemetryExtensions
{
    /// <summary>
    /// Logs estructurados (JSON con scopes) y trazas con OpenTelemetry. El exportador OTLP se activa solo si
    /// está definida la variable OTEL_EXPORTER_OTLP_ENDPOINT. Cada proceso agrega su propia instrumentación
    /// (por ejemplo, la API agrega la de ASP.NET Core) con <paramref name="configureTracing"/>, para que esta
    /// capa común no dependa del framework web.
    /// </summary>
    public static TBuilder AddNexgateTelemetry<TBuilder>(
        this TBuilder builder,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
        });

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => configureTracing?.Invoke(tracing));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            otel.UseOtlpExporter();
        }

        return builder;
    }
}
