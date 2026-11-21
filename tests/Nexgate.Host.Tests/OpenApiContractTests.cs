using System.Net;
using System.Text.Json.Nodes;

namespace Nexgate.Host.Tests;

/// <summary>
/// Contrato OpenAPI de la API v1 (platform-api: <c>API description document</c>, <c>Versioned API contract</c> e
/// <c>Interactive API reference in development</c>).
/// </summary>
public class OpenApiContractTests
{
    public const string UpdateVariable = "NEXGATE_UPDATE_OPENAPI";
    private const string DocumentPath = "/openapi/v1.json";

    private static readonly string[] Methods = ["get", "put", "post", "delete", "patch"];

    public static string ContractFile
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Nexgate.slnx")))
            {
                directory = directory.Parent;
            }

            return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("No se encontró Nexgate.slnx."),
                "api", "nexgate-v1.openapi.json");
        }
    }

    private static async Task<string> GenerateAsync()
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(DocumentPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Normalize(await response.Content.ReadAsStringAsync());
    }

    private static string Normalize(string json) => json.Replace("\r\n", "\n").TrimEnd() + "\n";

    private static IEnumerable<(string Path, string Method, JsonObject Operation)> Operations(JsonNode document) =>
        document["paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject()
                .Where(op => Methods.Contains(op.Key))
                .Select(op => (path.Key, op.Key, op.Value!.AsObject())));

    [Fact]
    public async Task Generated_document_matches_the_versioned_contract()
    {
        var generated = await GenerateAsync();

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ContractFile)!);
            await File.WriteAllTextAsync(ContractFile, generated);
            return;
        }

        Assert.True(File.Exists(ContractFile), $"Falta {ContractFile}. Generarlo con {UpdateVariable}=1 dotnet test --filter OpenApiContract.");
        var versioned = Normalize(await File.ReadAllTextAsync(ContractFile));
        if (versioned != generated)
        {
            Assert.Fail("La API cambió y el contrato versionado no se actualizó. Diferencias:\n" +
                string.Join('\n', Differences(JsonNode.Parse(versioned)!, JsonNode.Parse(generated)!)) +
                $"\nRevisar el cambio y regenerar con {UpdateVariable}=1 dotnet test --filter OpenApiContract.");
        }
    }

    /// <summary>Resumen legible: operaciones y esquemas agregados, quitados o cambiados.</summary>
    private static IEnumerable<string> Differences(JsonNode versioned, JsonNode generated)
    {
        var before = Operations(versioned).ToDictionary(o => $"{o.Method.ToUpperInvariant()} {o.Path}", o => o.Operation.ToJsonString());
        var after = Operations(generated).ToDictionary(o => $"{o.Method.ToUpperInvariant()} {o.Path}", o => o.Operation.ToJsonString());
        foreach (var line in Compare("operación", before, after))
        {
            yield return line;
        }

        var schemasBefore = (versioned["components"]?["schemas"]?.AsObject() ?? []).ToDictionary(s => s.Key, s => s.Value!.ToJsonString());
        var schemasAfter = (generated["components"]?["schemas"]?.AsObject() ?? []).ToDictionary(s => s.Key, s => s.Value!.ToJsonString());
        foreach (var line in Compare("esquema", schemasBefore, schemasAfter))
        {
            yield return line;
        }

        yield return "  (si no hay operaciones ni esquemas listados, cambió otra parte del documento)";
    }

    private static IEnumerable<string> Compare(string kind, Dictionary<string, string> before, Dictionary<string, string> after)
    {
        foreach (var key in after.Keys.Except(before.Keys).Order())
        {
            yield return $"  + {kind} {key}";
        }

        foreach (var key in before.Keys.Except(after.Keys).Order())
        {
            yield return $"  - {kind} {key}";
        }

        foreach (var key in before.Keys.Intersect(after.Keys).Where(k => before[k] != after[k]).Order())
        {
            yield return $"  ~ {kind} {key}";
        }
    }

    [Fact]
    public async Task Generation_is_stable()
    {
        Assert.Equal(await GenerateAsync(), await GenerateAsync());
    }

    [Fact]
    public async Task Every_operation_is_complete()
    {
        var document = JsonNode.Parse(await GenerateAsync())!;
        var operations = Operations(document).ToList();
        Assert.NotEmpty(operations);

        var ids = operations.Select(o => (string?)o.Operation["operationId"]).ToList();
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(ids.Count, ids.Distinct().Count());

        foreach (var (path, method, operation) in operations)
        {
            var name = $"{method.ToUpperInvariant()} {path}";
            Assert.StartsWith("/api/v1/", path);
            Assert.False(string.IsNullOrWhiteSpace((string?)operation["summary"]), $"{name} no tiene resumen.");
            Assert.Single(operation["tags"]!.AsArray());

            var responses = operation["responses"]!.AsObject();
            Assert.Contains(responses, r => r.Key.StartsWith('2'));
            Assert.True(responses.ContainsKey("500"), $"{name} no declara 500.");

            foreach (var (status, response) in responses)
            {
                if (status.StartsWith('4') || status.StartsWith('5'))
                {
                    var schema = (string?)response!["content"]?["application/problem+json"]?["schema"]?["$ref"];
                    Assert.True(schema is "#/components/schemas/ProblemDetails" or "#/components/schemas/HttpValidationProblemDetails",
                        $"{name} {status} no usa el formato estándar de errores.");
                }
                else if (status != "204")
                {
                    Assert.True(response!["content"]?["application/json"]?["schema"] is not null, $"{name} {status} no declara esquema.");
                }
            }

            var security = operation["security"]?.AsArray();
            var requiresToken = security is not null && security.All(r => r!.AsObject().ContainsKey(OpenApiBearer));
            if (security is not null)
            {
                Assert.True(responses.ContainsKey("401"), $"{name} acepta token y no declara 401.");
            }

            if (requiresToken)
            {
                Assert.True(responses.ContainsKey("401"), $"{name} exige token y no declara 401.");
            }
        }

        Assert.DoesNotContain(document["paths"]!.AsObject(), p => p.Key.Contains("health", StringComparison.OrdinalIgnoreCase));
    }

    private const string OpenApiBearer = "Bearer";

    [Theory]
    [InlineData("/api/v1/auth/token", "post", false)]
    [InlineData("/api/v1/auth/refresh", "post", false)]
    [InlineData("/api/v1/sites", "get", true)]
    [InlineData("/api/v1/platform/organizations", "post", true)]
    public async Task Operations_declare_whether_they_require_a_token(string path, string method, bool required)
    {
        var document = JsonNode.Parse(await GenerateAsync())!;
        var security = document["paths"]![path]![method]!["security"]?.AsArray();

        if (required)
        {
            Assert.NotNull(security);
            Assert.All(security, r => Assert.True(r!.AsObject().ContainsKey(OpenApiBearer)));
            Assert.Contains("403", document["paths"]![path]![method]!["responses"]!.AsObject().Select(r => r.Key));
        }
        else
        {
            Assert.Null(security);
        }
    }

    [Fact]
    public async Task Invitation_acceptance_accepts_an_optional_token()
    {
        var document = JsonNode.Parse(await GenerateAsync())!;
        var security = document["paths"]!["/api/v1/invitations/accept"]!["post"]!["security"]!.AsArray();

        Assert.Contains(security, r => r!.AsObject().Count == 0);
        Assert.Contains(security, r => r!.AsObject().ContainsKey(OpenApiBearer));
    }

    [Fact]
    public async Task Errors_declare_code_and_correlation_identifier()
    {
        var document = JsonNode.Parse(await GenerateAsync())!;
        foreach (var name in new[] { "ProblemDetails", "HttpValidationProblemDetails" })
        {
            var schema = document["components"]!["schemas"]![name]!;
            var required = schema["required"]!.AsArray().Select(r => (string?)r).ToList();
            Assert.Contains("code", required);
            Assert.Contains("correlationId", required);
        }

        Assert.NotNull(document["components"]!["schemas"]!["HttpValidationProblemDetails"]!["properties"]!["errors"]);
        Assert.Equal("bearer", (string?)document["components"]!["securitySchemes"]![OpenApiBearer]!["scheme"]);
        Assert.Null(document["servers"]);
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData(DocumentPath)]
    public async Task Reference_is_served_in_development(string path)
    {
        using var factory = new NexgateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger")]
    [InlineData(DocumentPath)]
    public async Task Reference_is_not_served_outside_development(string path)
    {
        using var factory = new NexgateFactory { Environment = "Production", SigningKey = NexgateFactory.NewSigningKey() };
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Protected_operation_called_as_from_the_reference_goes_through_permissions()
    {
        // Lo que hace Swagger UI: la misma API con el encabezado Authorization del usuario.
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        var email = NexgateFactory.NewEmail();
        var invitation = await factory.InviteAsync(organization, email, "guard");
        var guard = await factory.AcceptAndSignInAsync(invitation.Token, email, organization);
        using var client = factory.CreateClientWithToken(guard.Tokens.AccessToken);

        using var listed = await client.GetAsync("/api/v1/sites");
        using var denied = await client.GetAsync("/api/v1/members");

        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
