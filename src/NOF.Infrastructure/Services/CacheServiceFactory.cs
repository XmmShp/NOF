using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure;

/// <summary>
/// Creates named <see cref="ICacheService"/> instances, mirroring the <c>IHttpClientFactory</c> pattern.
/// Resolve this from DI and call <see cref="CreateCacheService(string)"/> to obtain a named cache instance.
/// </summary>
public interface ICacheServiceFactory
{
    /// <summary>
    /// The name used for the default (unnamed) cache service registration.
    /// </summary>
    const string DefaultName = "";

    /// <summary>
    /// Creates a named <see cref="ICacheService"/> instance.
    /// </summary>
    /// <param name="name">The logical name of the cache service registration.</param>
    /// <returns>The <see cref="ICacheService"/> for the given name.</returns>
    ICacheService CreateCacheService(string name);

    /// <summary>
    /// Creates the default <see cref="ICacheService"/> instance (registered under <see cref="DefaultName"/>).
    /// </summary>
    ICacheService CreateCacheService() => CreateCacheService(DefaultName);
}

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
