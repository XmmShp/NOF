using Microsoft.Extensions.DependencyInjection;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Default implementation of <see cref="ICacheServiceBuilder"/>.
/// Returned by <c>AddCacheService</c> for fluent configuration of a named registration.
/// Serializer and lock retry strategy are registered as keyed DI services under <see cref="Name"/>.
/// </summary>
public sealed class CacheServiceBuilder : ICacheServiceBuilder
{
    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public IServiceCollection Services { get; }

    public CacheServiceBuilder(string name, IServiceCollection services)
    {
        Name = name;
        Services = services;
    }
}
