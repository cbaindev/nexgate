using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Nexgate.BuildingBlocks.Tenancy;

namespace Nexgate.Host.Tests;

/// <summary>
/// Levanta la API real en memoria contra PostgreSQL de verdad: la base de pruebas de <see cref="TestDatabase"/>,
/// conectada con el rol de la aplicación. Corre en el entorno Development con el mecanismo de desarrollo de
/// tenancy habilitado (encabezado X-Nexgate-Organization y endpoints de plataforma), salvo que se indique lo contrario.
/// </summary>
public sealed class NexgateFactory(TestDatabase? database = null) : WebApplicationFactory<Program>
{
    private readonly TestDatabase _database = database ?? Task.Run(TestDatabase.GetAsync).GetAwaiter().GetResult();

    public LogCapture Logs { get; } = new();

    /// <summary>Reemplaza la conexión de la aplicación (por ejemplo, una base caída).</summary>
    public string? ConnectionString { get; init; }

    /// <summary>Fija la organización activa sin pasar por el middleware (pruebas de telemetría).</summary>
    public Guid? Organization { get; init; }

    public bool DevelopmentAccess { get; init; } = true;

    public string Environment { get; init; } = "Development";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        builder.UseSetting("Tenancy:DevelopmentAccess:Enabled", DevelopmentAccess ? "true" : "false");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString ?? _database.AppConnectionString);
        builder.UseSetting("ConnectionStrings:Migrations", _database.OwnerConnectionString);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(Logs);
            services.AddSingleton<IStartupFilter, TestEndpoints>();

            if (Organization is { } id)
            {
                services.RemoveAll<ICurrentOrganization>();
                services.AddSingleton<ICurrentOrganization>(new FixedOrganization(id));
            }
        });
    }

    /// <summary>Cliente HTTP que actúa en nombre de la organización indicada (mecanismo de desarrollo).</summary>
    public HttpClient CreateClientFor(Guid organizationId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(OrganizationResolutionMiddleware.DevelopmentHeader, organizationId.ToString());
        return client;
    }

    /// <summary>Crea una organización por el endpoint de plataforma y devuelve su identificador.</summary>
    public async Task<Guid> CreateOrganizationAsync(string name = "Organización de prueba")
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/platform/organizations", new { name });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<IdOnly>();
        return created!.Id;
    }

    /// <summary>Una base de datos que no existe: el puerto 1 rechaza la conexión enseguida.</summary>
    public const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=nexgate;Username=nexgate;Password=super-secreto-123;Timeout=2";

    private sealed record IdOnly(Guid Id);

    private sealed class FixedOrganization(Guid id) : ICurrentOrganization
    {
        public Guid? Id => id;
    }
}
