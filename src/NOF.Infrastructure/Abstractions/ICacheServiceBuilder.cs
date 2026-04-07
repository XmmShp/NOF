using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

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
}

public static partial class NOFInfrastructureExtensions
{
    extension(ICacheServiceBuilder builder)
    {
        /// <summary>Uses a pre-created serializer instance.</summary>
        public ICacheServiceBuilder WithSerializer(ICacheSerializer serializer)
        {
            ArgumentNullException.ThrowIfNull(serializer);
            builder.Services.AddKeyedScoped(builder.Name, (_, _) => serializer);
            return builder;
        }

        /// <summary>Uses a factory to create the serializer from DI.</summary>
        public ICacheServiceBuilder WithSerializer(Func<IServiceProvider, ICacheSerializer> factory)
        {
            ArgumentNullException.ThrowIfNull(factory);
            builder.Services.AddKeyedScoped(builder.Name, (sp, _) => factory(sp));
            return builder;
        }

        /// <summary>Uses a DI-resolved serializer of type <typeparamref name="T"/>.</summary>
        public ICacheServiceBuilder WithSerializer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, ICacheSerializer
        {
            builder.Services.AddKeyedScoped<ICacheSerializer, T>(builder.Name);
            return builder;
        }

        /// <summary>Uses a pre-created lock retry strategy instance.</summary>
        public ICacheServiceBuilder WithLockRetryStrategy(ICacheLockRetryStrategy strategy)
        {
            ArgumentNullException.ThrowIfNull(strategy);
            builder.Services.AddKeyedScoped(builder.Name, (_, _) => strategy);
            return builder;
        }

        /// <summary>Uses a factory to create the lock retry strategy from DI.</summary>
        public ICacheServiceBuilder WithLockRetryStrategy(Func<IServiceProvider, ICacheLockRetryStrategy> factory)
        {
            ArgumentNullException.ThrowIfNull(factory);
            builder.Services.AddKeyedScoped(builder.Name, (sp, _) => factory(sp));
            return builder;
        }

        /// <summary>Uses a DI-resolved lock retry strategy of type <typeparamref name="T"/>.</summary>
        public ICacheServiceBuilder WithLockRetryStrategy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, ICacheLockRetryStrategy
        {
            builder.Services.AddKeyedScoped<ICacheLockRetryStrategy, T>(builder.Name);
            return builder;
        }
    }
}
