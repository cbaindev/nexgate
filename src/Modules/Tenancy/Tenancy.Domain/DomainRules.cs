namespace Nexgate.Modules.Tenancy.Domain;

/// <summary>Una regla del dominio no se cumple. El campo usa el nombre de la propiedad pública.</summary>
public sealed class DomainValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

internal static class DomainRules
{
    public static string RequiredText(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(field, "El valor es obligatorio.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainValidationException(field, $"El valor no puede superar {maxLength} caracteres.");
        }

        return trimmed;
    }
}
