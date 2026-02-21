using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Default implementation of <see cref="ICacheServiceFactory"/>.
/// Resolves named <see cref="ICacheService"/> instances via keyed DI.
/// </summary>
internal sealed class DefaultCacheServiceFactory : ICacheServiceFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DefaultCacheServiceFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ICacheService CreateCacheService(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _serviceProvider.GetRequiredKeyedService<ICacheService>(name);
    }
}
