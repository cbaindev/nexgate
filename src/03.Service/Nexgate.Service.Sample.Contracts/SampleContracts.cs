using System.ComponentModel.DataAnnotations;

namespace Nexgate.Service.Sample.Contracts;

/// <summary>Contrato público del módulo de ejemplo. Otros módulos y la API solo pueden depender de este proyecto.</summary>
public interface IGreetingService
{
    GreetingDto Greet(string name);
}

public sealed record GreetingDto(string Message);

/// <summary>
/// Modelo de entrada del saludo. Las reglas de validación viajan con el contrato; en un record van sobre el
/// parámetro del constructor (MVC rechaza los atributos de validación puestos en la propiedad).
/// </summary>
public sealed record GreetRequest(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(50, ErrorMessage = "El nombre no puede superar 50 caracteres")]
    string? Name);
