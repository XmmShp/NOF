using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// A builder for configuring a named <see cref="ICacheService"/> registration.
/// Mirrors <c>IHttpClientBuilder</c>.
/// </summary>
public interface ICacheServiceBuilder
{
    /// <summary>The logical name of this cache service registration.</summary>
    string Name { get; }

    /// <summary>The service collection backing this builder.</summary>
    IServiceCollection Services { get; }

    /// <summary>Uses a pre-created serializer instance.</summary>
    ICacheServiceBuilder WithSerializer(ICacheSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        Services.AddKeyedScoped<ICacheSerializer>(Name, (_, _) => serializer);
        return this;
    }

    /// <summary>Uses a factory to create the serializer from DI.</summary>
    ICacheServiceBuilder WithSerializer(Func<IServiceProvider, ICacheSerializer> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Services.AddKeyedScoped<ICacheSerializer>(Name, (sp, _) => factory(sp));
        return this;
    }

    /// <summary>Uses a DI-resolved serializer of type <typeparamref name="T"/>.</summary>
    ICacheServiceBuilder WithSerializer<T>() where T : class, ICacheSerializer
    {
        Services.AddKeyedScoped<ICacheSerializer, T>(Name);
        return this;
    }

    /// <summary>Uses a pre-created lock retry strategy instance.</summary>
    ICacheServiceBuilder WithLockRetryStrategy(ICacheLockRetryStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        Services.AddKeyedScoped<ICacheLockRetryStrategy>(Name, (_, _) => strategy);
        return this;
    }

    /// <summary>Uses a factory to create the lock retry strategy from DI.</summary>
    ICacheServiceBuilder WithLockRetryStrategy(Func<IServiceProvider, ICacheLockRetryStrategy> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Services.AddKeyedScoped<ICacheLockRetryStrategy>(Name, (sp, _) => factory(sp));
        return this;
    }

    /// <summary>Uses a DI-resolved lock retry strategy of type <typeparamref name="T"/>.</summary>
    ICacheServiceBuilder WithLockRetryStrategy<T>() where T : class, ICacheLockRetryStrategy
    {
        Services.AddKeyedScoped<ICacheLockRetryStrategy, T>(Name);
        return this;
    }
}
