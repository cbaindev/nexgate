using System.Text.Json;
using Nexgate.Domain.Access;
using Nexgate.Service.Access.Contracts;

namespace Nexgate.Test.Api;

/// <summary>
/// Vectores compartidos con el firmware (vectors/access-evaluation/v1): cada archivo es una política compilada, una
/// credencial, un instante y la decisión esperada.
/// </summary>
public class AccessVectorsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Expected(string Decision, string Reason);

    private sealed record Vector(string Description, CompiledPolicy Policy, string CredentialHash, DateTimeOffset At, Expected Expected);

    private static string Folder()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "vectors", "access-evaluation", "v1");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("No se encontró vectors/access-evaluation/v1 en el repositorio.");
    }

    public static TheoryData<string> Files()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Folder(), "*.json").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(file));
        }

        return data;
    }

    [Fact]
    public void The_vector_set_is_not_empty_and_covers_every_reason()
    {
        var vectors = Directory.GetFiles(Folder(), "*.json").Select(f => JsonSerializer.Deserialize<Vector>(File.ReadAllText(f), Json)!).ToList();

        Assert.NotEmpty(vectors);
        Assert.Equal(
            [AccessReasonNames.Allowed, AccessReasonNames.CredentialInvalid, AccessReasonNames.ExplicitDeny, AccessReasonNames.NoPermission],
            vectors.Select(v => v.Expected.Reason).Distinct().Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Every_vector_produces_its_expected_decision(string file)
    {
        var vector = JsonSerializer.Deserialize<Vector>(File.ReadAllText(Path.Combine(Folder(), file)), Json)!;

        var result = AccessDecision.Evaluate(vector.Policy, vector.CredentialHash, vector.At);

        Assert.True(vector.Expected.Decision == result.Decision && vector.Expected.Reason == result.Reason,
            $"{file} ({vector.Description}): se esperaba {vector.Expected.Decision}/{vector.Expected.Reason} y dio {result.Decision}/{result.Reason}");
    }
}
