using Microsoft.Extensions.DependencyInjection;

namespace NOF.Infrastructure.Core;

public sealed class InitializingServiceScopeFactory(IServiceScopeFactory innerScopeFactory) : IServiceScopeFactory
{
    public IServiceScope CreateScope()
        => new InitializingServiceScope(innerScopeFactory.CreateScope());
}
