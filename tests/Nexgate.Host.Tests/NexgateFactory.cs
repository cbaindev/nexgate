using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Nexgate.BuildingBlocks.Tenancy;
using Nexgate.Modules.Identity.Application;

namespace Nexgate.Host.Tests;

public sealed record TokenSet(string AccessToken, string TokenType, int ExpiresIn, string RefreshToken, Guid? OrganizationId);

/// <summary>Usuario de prueba creado por el flujo real (invitación, aceptación e inicio de sesión).</summary>
public sealed record TestUser(Guid UserId, string Email, string Password, TokenSet Tokens);

/// <summary>
/// Levanta la API real en memoria contra PostgreSQL de verdad: la base de pruebas de <see cref="TestDatabase"/>,
/// conectada con el rol de la aplicación. Corre en Development (clave de firma efímera). Los ayudantes obtienen tokens
/// por el flujo real: operador de plataforma, alta de organización, aceptación de la invitación e inicio de sesión.
/// </summary>
public sealed class NexgateFactory(TestDatabase? database = null) : WebApplicationFactory<Program>
{
    public const string Password = "contraseña-de-prueba-123";

    private readonly TestDatabase _database = database ?? Task.Run(TestDatabase.GetAsync).GetAwaiter().GetResult();
    private readonly ConcurrentDictionary<Guid, TestUser> _administrators = new();
    private readonly SemaphoreSlim _operatorLock = new(1, 1);
    private TestUser? _operator;

    public LogCapture Logs { get; } = new();

    /// <summary>Respuestas observadas que el contrato OpenAPI no declara; la fábrica falla al liberarse si hay alguna.</summary>
    public ResponseContractRecorder Responses { get; } = new();

    /// <summary>Reemplaza la conexión de la aplicación (por ejemplo, una base caída).</summary>
    public string? ConnectionString { get; init; }

    /// <summary>Fija la organización activa sin pasar por el middleware (pruebas de telemetría).</summary>
    public Guid? Organization { get; init; }

    public string Environment { get; init; } = "Development";

    /// <summary>Clave de firma en PEM; sin valor, Development usa una efímera.</summary>
    public string? SigningKey { get; init; }

    /// <summary>Intentos por minuto del limitador. Alto por defecto para que las pruebas no lo choquen.</summary>
    public int RateLimitPermits { get; init; } = 100_000;

    public static string NewSigningKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return key.ExportECPrivateKeyPem();
    }

    public static string NewEmail(string prefix = "usuario") => $"{prefix}-{Guid.NewGuid():N}@nexgate.test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        builder.UseSetting("ConnectionStrings:Default", ConnectionString ?? _database.AppConnectionString);
        builder.UseSetting("ConnectionStrings:Migrations", _database.OwnerConnectionString);
        builder.UseSetting("Identity:Passwords:IterationCount", "1000");
        builder.UseSetting("Identity:RateLimit:PermitsPerMinute", RateLimitPermits.ToString());
        if (SigningKey is not null)
        {
            builder.UseSetting("Identity:Tokens:SigningKey", SigningKey);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(Logs);
            services.AddSingleton<IStartupFilter, TestEndpoints>();
            services.AddSingleton<IStartupFilter>(Responses);

            if (Organization is { } id)
            {
                services.RemoveAll<ICurrentOrganization>();
                services.AddSingleton<ICurrentOrganization>(new FixedOrganization(id));
            }
        });
    }

    public HttpClient CreateClientWithToken(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>Cliente HTTP del administrador de la organización (creada con <see cref="CreateOrganizationAsync"/>).</summary>
    public HttpClient CreateClientFor(Guid organizationId) => CreateClientWithToken(Administrator(organizationId).Tokens.AccessToken);

    public TestUser Administrator(Guid organizationId) => _administrators[organizationId];

    public async Task<HttpResponseMessage> PostSignInAsync(string email, string password, Guid? organizationId = null)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync("/api/v1/auth/token", new { email, password, organizationId });
    }

    public async Task<TokenSet> SignInAsync(string email, string password = Password, Guid? organizationId = null)
    {
        var response = await PostSignInAsync(email, password, organizationId);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenSet>())!;
    }

    /// <summary>Operador de plataforma de esta fábrica, creado con el mismo servicio que usa el comando del host.</summary>
    public async Task<TestUser> OperatorAsync()
    {
        await _operatorLock.WaitAsync();
        try
        {
            if (_operator is null)
            {
                var email = NewEmail("operador");
                await using (var scope = Services.CreateAsyncScope())
                {
                    var result = await scope.ServiceProvider.GetRequiredService<PlatformOperatorService>().EnsureAsync(email, Password, CancellationToken.None);
                    Assert.Null(result.Error);
                }

                var tokens = await SignInAsync(email);
                _operator = new TestUser(Guid.Empty, email, Password, tokens);
            }

            return _operator;
        }
        finally
        {
            _operatorLock.Release();
        }
    }

    public async Task<HttpClient> CreateOperatorClientAsync() => CreateClientWithToken((await OperatorAsync()).Tokens.AccessToken);

    /// <summary>
    /// Crea una organización por el endpoint de plataforma, acepta la invitación de su administrador e inicia sesión
    /// con él. Devuelve el identificador de la organización.
    /// </summary>
    public async Task<Guid> CreateOrganizationAsync(string name = "Organización de prueba")
    {
        using var operatorClient = await CreateOperatorClientAsync();
        var email = NewEmail("admin");
        var response = await operatorClient.PostAsJsonAsync("/api/v1/platform/organizations", new { name, administratorEmail = email });
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<CreatedOrganization>())!;

        var user = await AcceptAndSignInAsync(created.AdministratorInvitation.Token, email, created.Id);
        _administrators[created.Id] = user;
        return created.Id;
    }

    /// <summary>Invita (como administrador de la organización) a un usuario nuevo con el rol y alcance indicados.</summary>
    public async Task<TestUser> CreateUserAsync(Guid organizationId, string role, string scopeType = "organization", Guid? siteId = null)
    {
        var token = await InviteAsync(organizationId, NewEmail(role), role, scopeType, siteId);
        return await AcceptAndSignInAsync(token.Token, token.Email, organizationId);
    }

    public async Task<InvitationToken> InviteAsync(Guid organizationId, string email, string role, string scopeType = "organization", Guid? siteId = null)
    {
        using var admin = CreateClientFor(organizationId);
        var response = await admin.PostAsJsonAsync("/api/v1/invitations", new { email, role, scopeType, siteId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvitationToken>())!;
    }

    public async Task<TestUser> AcceptAndSignInAsync(string invitationToken, string email, Guid organizationId)
    {
        using var client = CreateClient();
        var accept = await client.PostAsJsonAsync("/api/v1/invitations/accept", new { token = invitationToken, password = Password });
        accept.EnsureSuccessStatusCode();
        var accepted = (await accept.Content.ReadFromJsonAsync<Accepted>())!;
        var tokens = await SignInAsync(email, Password, organizationId);
        return new TestUser(accepted.UserId, email, Password, tokens);
    }

    /// <summary>Una base de datos que no existe: el puerto 1 rechaza la conexión enseguida.</summary>
    public const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=nexgate;Username=nexgate;Password=super-secreto-123;Timeout=2";

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _operatorLock.Dispose();
        }

        base.Dispose(disposing);

        if (disposing && Responses.Undeclared.Count > 0)
        {
            throw new InvalidOperationException("Respuestas no declaradas en el contrato OpenAPI (agregar Produces o Problems al " +
                "endpoint y regenerar api/nexgate-v1.openapi.json):\n" + string.Join('\n', Responses.Undeclared.Distinct().Order()));
        }
    }

    public sealed record InvitationToken(Guid Id, string Email, string Role, string ScopeType, Guid? SiteId, DateTimeOffset ExpiresAt, string Token);

    private sealed record CreatedOrganization(Guid Id, string Name, string Status, InvitationToken AdministratorInvitation);

    private sealed record Accepted(Guid UserId, Guid OrganizationId);

    private sealed class FixedOrganization(Guid id) : ICurrentOrganization
    {
        public Guid? Id => id;
    }
}
