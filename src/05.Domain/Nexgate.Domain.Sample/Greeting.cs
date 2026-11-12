namespace Nexgate.Domain.Sample;

public sealed class Greeting
{
    private Greeting(string message) => Message = message;

    public string Message { get; }

    public static Greeting For(string name) => new($"Hola, {name.Trim()}");
}
