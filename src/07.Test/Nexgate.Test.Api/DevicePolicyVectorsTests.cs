using System.Security.Cryptography;
using System.Text.Json;
using Nexgate.Domain.Devices;

namespace Nexgate.Test.Api;

/// <summary>
/// Vectores de firma compartidos con el firmware (vectors/device-policy/v1): bytes exactos de un paquete, una firma y si
/// la verificación tiene que dar válida.
/// </summary>
public class DevicePolicyVectorsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Vector(string Description, string PublicKey, string Package, string Signature, bool Valid);

    private static string Folder()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "vectors", "device-policy", "v1");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("No se encontró vectors/device-policy/v1 en el repositorio.");
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

    private static Vector Load(string file) => JsonSerializer.Deserialize<Vector>(File.ReadAllText(Path.Combine(Folder(), file)), Json)!;

    [Fact]
    public void The_vector_set_has_valid_and_invalid_cases()
    {
        var vectors = Directory.GetFiles(Folder(), "*.json").Select(f => Load(Path.GetFileName(f))).ToList();

        Assert.Contains(vectors, v => v.Valid);
        Assert.Contains(vectors, v => !v.Valid);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Each_vector_verifies_as_expected(string file)
    {
        var vector = Load(file);
        using var key = ECDsa.Create();
        key.ImportFromPem(vector.PublicKey);
        var content = Convert.FromBase64String(vector.Package);

        Assert.True(vector.Valid == PolicySignature.Verify(key, content, vector.Signature), vector.Description);
        using var package = JsonDocument.Parse(content);
        Assert.Equal(1, package.RootElement.GetProperty("format").GetInt32());
    }
}
