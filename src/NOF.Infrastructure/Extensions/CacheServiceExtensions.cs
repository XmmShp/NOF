using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Application;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

public static partial class NOFInfrastructureExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers a hosted service that executes an asynchronous delegate when the host starts.
        /// The delegate receives the <see cref="IServiceProvider"/> and a <see cref="CancellationToken"/>
        /// for graceful shutdown, and runs as a background task managed by the host lifetime.
        /// </summary>
        /// <param name="startAction">
        /// An asynchronous function invoked during the hosted service's <c>StartAsync</c> phase.
        /// It should honor the cancellation token and avoid blocking indefinitely.
        /// </param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection AddHostedService(Func<IServiceProvider, CancellationToken, Task> startAction)
        {
            return services.AddHostedService(sp => new DelegateBackgroundService(sp, startAction));
        }

        /// <summary>
        /// Registers a hosted service that executes a synchronous delegate when the host starts.
        /// The delegate receives the <see cref="IServiceProvider"/> and a <see cref="CancellationToken"/>,
        /// and is wrapped in a completed task to integrate with the async hosted service lifecycle.
        /// </summary>
        /// <param name="startAction">
        /// A synchronous action invoked during the hosted service's <c>StartAsync</c> phase.
        /// Although executed synchronously, it must still respect the cancellation token
        /// and complete promptly to avoid delaying application startup or shutdown.
        /// </param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
        public IServiceCollection AddHostedService(Action<IServiceProvider, CancellationToken> startAction)
            => services.AddHostedService((sp, ct) => { startAction(sp, ct); return Task.CompletedTask; });

        /// <summary>
        /// Registers a named <see cref="ICacheService"/> using a typed implementation.
        /// Uses keyed DI: the implementation, serializer, lock retry strategy, and options are all
        /// registered under <paramref name="name"/> as the service key.
        /// Defaults to <see cref="JsonCacheSerializer"/> and <see cref="ExponentialBackoffCacheLockRetryStrategy"/>
        /// unless overridden via the returned <see cref="ICacheServiceBuilder"/>.
        /// </summary>
        /// <typeparam name="TImplementation">The <see cref="ICacheService"/> implementation type.</typeparam>
        /// <param name="name">The logical name / service key for this registration.</param>
        /// <param name="configure">Optional action to configure <see cref="CacheServiceOptions"/> for this name.</param>
        /// <returns>An <see cref="ICacheServiceBuilder"/> for further configuration.</returns>
        public ICacheServiceBuilder AddCacheService<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            string name = ICacheServiceFactory.DefaultName,
            Action<CacheServiceOptions>? configure = null)
            where TImplementation : class, ICacheService
        {
            ArgumentNullException.ThrowIfNull(name);

            // Named options for value config (KeyPrefix, DefaultEntryOptions, etc.)
            services.AddOptions<CacheServiceOptions>(name);
            if (configure is not null)
            {
                services.Configure(name, configure);
            }

            services.AddKeyedScoped<IOptions<CacheServiceOptions>>(name, (sp, key) =>
                Options.Create(sp.GetRequiredService<IOptionsMonitor<CacheServiceOptions>>().Get((string)key!)));

            // Keyed cache service factory explicitly resolves keyed deps and named options
            services.AddKeyedScoped<ICacheService>(name, (sp, key) =>
            {
                var serializer = sp.GetKeyedService<ICacheSerializer>(key) ?? sp.GetRequiredService<ICacheSerializer>();
                var lockRetryStrategy = sp.GetKeyedService<ICacheLockRetryStrategy>(key) ?? sp.GetRequiredService<ICacheLockRetryStrategy>();
                var opts = sp.GetRequiredKeyedService<IOptions<CacheServiceOptions>>(key!);
                return ActivatorUtilities.CreateInstance<TImplementation>(sp, serializer, lockRetryStrategy, opts);
            });

            services.AddKeyedScoped<IDistributedCache>(name, (sp, key) =>
                sp.GetRequiredKeyedService<ICacheService>(key!));

            return new CacheServiceBuilder(name, services);
        }

        public ICacheServiceBuilder TryAddCacheService<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            string name = ICacheServiceFactory.DefaultName,
            Action<CacheServiceOptions>? configure = null)
            where TImplementation : class, ICacheService
        {
            ArgumentNullException.ThrowIfNull(name);

            var hasRegistration = services.Any(service =>
                service.ServiceType == typeof(ICacheService) &&
                Equals(service.ServiceKey, name));

            if (!hasRegistration)
            {
                return services.AddCacheService<TImplementation>(name, configure);
            }

            return new CacheServiceBuilder(name, services);
        }
    }
}
