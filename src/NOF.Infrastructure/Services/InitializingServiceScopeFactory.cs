using Microsoft.Extensions.DependencyInjection;

namespace NOF.Infrastructure;

public sealed class InitializingServiceScopeFactory(IServiceScopeFactory innerScopeFactory) : IServiceScopeFactory
{
    public IServiceScope CreateScope()
        => new InitializingServiceScope(innerScopeFactory.CreateScope());
}
