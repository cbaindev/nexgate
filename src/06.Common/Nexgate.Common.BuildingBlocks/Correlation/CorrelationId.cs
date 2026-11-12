using System.Text.RegularExpressions;

namespace Nexgate.Common.BuildingBlocks.Correlation;

/// <summary>Convenciones del identificador de correlación de cada solicitud.</summary>
public static partial class CorrelationId
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "nexgate.correlation-id";
    public const int MaxLength = 64;

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex SafeCharacters();

    /// <summary>Un valor entrante es aceptable si es corto y usa solo caracteres seguros.</summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= MaxLength && SafeCharacters().IsMatch(value);

    public static string Generate() => Guid.NewGuid().ToString("N");
}
