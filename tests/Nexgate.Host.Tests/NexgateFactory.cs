using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Nexgate.BuildingBlocks.Tenancy;

namespace Nexgate.Host.Tests;

/// <summary>
/// Levanta la API real en memoria. Usa PostgreSQL de verdad: la cadena de conexión sale de
/// NEXGATE_TEST_PG, o de appsettings.json (la que usa docker-compose) si no está definida.
/// </summary>
public sealed class NexgateFactory : WebApplicationFactory<Program>
{
    public LogCapture Logs { get; } = new();

    public string? ConnectionString { get; init; }

    public Guid? Organization { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Sample:EnableFailureEndpoint", "true");

        var connectionString = ConnectionString ?? Environment.GetEnvironmentVariable("NEXGATE_TEST_PG");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            builder.UseSetting("ConnectionStrings:Default", connectionString);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(Logs);

            if (Organization is { } id)
            {
                services.RemoveAll<ICurrentOrganization>();
                services.AddSingleton<ICurrentOrganization>(new FixedOrganization(id));
            }
        });
    }

    /// <summary>Una base de datos que no existe: el puerto 1 rechaza la conexión enseguida.</summary>
    public const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=nexgate;Username=nexgate;Password=super-secreto-123;Timeout=2";

    private sealed class FixedOrganization(Guid id) : ICurrentOrganization
    {
        public Guid? Id => id;
    }
}
