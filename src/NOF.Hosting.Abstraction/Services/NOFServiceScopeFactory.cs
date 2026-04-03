using Microsoft.Extensions.DependencyInjection;

namespace NOF.Hosting;

public sealed class NOFServiceScopeFactory(IServiceScopeFactory innerScopeFactory) : IServiceScopeFactory
{
    public IServiceScope CreateScope()
        => new NOFServiceScope(innerScopeFactory.CreateScope());
}
