using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Api.Rest.Models.Validation;

/// <summary>Zona horaria IANA válida según la base tzdb del módulo Tenancy (no depende del sistema operativo).</summary>
public sealed class IanaTimeZoneAttribute() : ValidationAttribute("Zona horaria IANA inválida (por ejemplo, America/Argentina/Cordoba)")
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        var catalog = (ITimeZoneCatalog?)validationContext.GetService(typeof(ITimeZoneCatalog))
            ?? throw new InvalidOperationException("ITimeZoneCatalog no está registrado.");
        return catalog.IsValid(value as string)
            ? ValidationResult.Success
            : new ValidationResult(FormatErrorMessage(validationContext.DisplayName),
                validationContext.MemberName is { } member ? [member] : null);
    }
}

public sealed class JsonObjectOrNullAttribute() : ValidationAttribute("La configuración debe ser un objeto JSON")
{
    public override bool IsValid(object? value) =>
        value is null || value is JsonElement { ValueKind: JsonValueKind.Object or JsonValueKind.Null or JsonValueKind.Undefined };
}

/// <summary>Correo válido según las reglas de cuentas de Identity.</summary>
public sealed class AccountEmailAttribute() : ValidationAttribute("Correo electrónico inválido")
{
    public override bool IsValid(object? value) => value is null || EmailRules.TryNormalize(value as string, out _);
}
