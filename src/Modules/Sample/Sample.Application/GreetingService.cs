using Nexgate.Modules.Sample.Contracts;
using Nexgate.Modules.Sample.Domain;

namespace Nexgate.Modules.Sample.Application;

public sealed class GreetingService : IGreetingService
{
    public GreetingDto Greet(string name) => new(Greeting.For(name).Message);
}
