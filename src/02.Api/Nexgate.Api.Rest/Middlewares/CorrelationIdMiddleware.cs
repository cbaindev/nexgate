using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Nexgate.Common.BuildingBlocks.Correlation;
using Nexgate.Common.BuildingBlocks.Telemetry;

namespace Nexgate.Api.Rest.Middlewares;

/// <summary>
/// Asigna un identificador de correlación a cada solicitud: reutiliza el que manda el cliente si es válido,
/// lo devuelve en la respuesta y lo incluye en los logs y trazas de esa solicitud.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationId.HeaderName].ToString();
        var correlationId = CorrelationId.IsValid(incoming) ? incoming : CorrelationId.Generate();

        context.Items[CorrelationId.ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationId.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        Activity.Current?.SetTag("correlation.id", correlationId);

        using (logger.BeginScope(new LogScope("CorrelationId", correlationId)))
        {
            await next(context);
        }
    }
}

public static class CorrelationIdHttpContextExtensions
{
    public static string? GetCorrelationId(this HttpContext context) =>
        context.Items.TryGetValue(CorrelationId.ItemKey, out var value) ? value as string : null;
}
