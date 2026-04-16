using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure;

/// <summary>
/// Default implementation of <see cref="ICacheServiceFactory"/>.
/// Resolves named <see cref="ICacheService"/> instances via keyed DI.
/// </summary>
public sealed class CacheServiceFactory : ICacheServiceFactory
{
    private readonly IServiceProvider _serviceProvider;

    public CacheServiceFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ICacheService CreateCacheService(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _serviceProvider.GetRequiredKeyedService<ICacheService>(name);
    }
}
