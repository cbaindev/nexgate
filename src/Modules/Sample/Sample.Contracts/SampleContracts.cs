namespace Nexgate.Modules.Sample.Contracts;

/// <summary>Contrato público del módulo de ejemplo. Otros módulos solo pueden depender de este proyecto.</summary>
public interface IGreetingService
{
    GreetingDto Greet(string name);
}

public sealed record GreetingDto(string Message);
