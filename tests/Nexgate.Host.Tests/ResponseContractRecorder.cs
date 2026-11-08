using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Nexgate.Host.Tests;

/// <summary>
/// Verifica que cada respuesta de un endpoint de negocio que producen las pruebas de integración esté declarada en sus
/// metadatos, que son la fuente del documento OpenAPI (platform-api, <c>API description document</c>; design de
/// add-openapi-contract, decisión 5). Las respuestas no declaradas se acumulan y la fábrica falla al liberarse, así
/// la prueba que la produjo queda en rojo.
/// </summary>
public sealed class ResponseContractRecorder : IStartupFilter
{
    private readonly ConcurrentQueue<string> _undeclared = new();

    public IReadOnlyCollection<string> Undeclared => _undeclared;

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, call) =>
        {
            await call(context);
            Check(context);
        });
        next(app);
    };

    private void Check(HttpContext context)
    {
        if (context.GetEndpoint() is not RouteEndpoint endpoint
            || endpoint.RoutePattern.RawText?.StartsWith("/api/v1/", StringComparison.Ordinal) != true)
        {
            return;
        }

        var declared = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>().Select(m => m.StatusCode).ToHashSet();
        if (!declared.Contains(context.Response.StatusCode))
        {
            _undeclared.Enqueue($"{context.Request.Method} {endpoint.RoutePattern.RawText} -> {context.Response.StatusCode}");
        }
    }
}
