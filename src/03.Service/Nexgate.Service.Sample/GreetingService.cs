using Nexgate.Service.Sample.Contracts;
using Nexgate.Domain.Sample;

namespace Nexgate.Service.Sample;

internal sealed class GreetingService : IGreetingService
{
    public GreetingDto Greet(string name) => new(Greeting.For(name).Message);
}
