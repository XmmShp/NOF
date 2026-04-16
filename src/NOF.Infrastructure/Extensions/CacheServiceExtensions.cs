using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Hosting;
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
        /// Registers the current <see cref="ICacheService"/> using a typed implementation.
        /// Defaults to <see cref="JsonObjectSerializer"/> and <see cref="ExponentialBackoffCacheLockRetryStrategy"/>.
        /// </summary>
        /// <typeparam name="TImplementation">The <see cref="ICacheService"/> implementation type.</typeparam>
        /// <param name="configure">Optional action to configure <see cref="CacheServiceOptions"/>.</param>
        public IServiceCollection AddCacheService<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            Action<CacheServiceOptions>? configure = null)
            where TImplementation : class, ICacheService
        {
            services.AddOptions<CacheServiceOptions>();
            if (configure is not null)
            {
                services.Configure(configure);
            }

            services.ReplaceOrAddScoped<TImplementation>(sp =>
            {
                var serializer = sp.GetRequiredService<IObjectSerializer>();
                var lockRetryStrategy = sp.GetRequiredService<ICacheLockRetryStrategy>();
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CacheServiceOptions>>();
                return ActivatorUtilities.CreateInstance<TImplementation>(sp, serializer, lockRetryStrategy, opts);
            });
            services.ReplaceOrAddScoped<ICacheService>(sp => sp.GetRequiredService<TImplementation>());
            services.ReplaceOrAddScoped<IDistributedCache>(sp => sp.GetRequiredService<ICacheService>());

            return services;
        }

        public IServiceCollection ReplaceOrAddCacheService<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            Action<CacheServiceOptions>? configure = null)
            where TImplementation : class, ICacheService
        {
            var descriptorsToRemove = services
                .Where(service =>
                    service.ServiceType == typeof(IDistributedCache) ||
                    typeof(ICacheService).IsAssignableFrom(service.ServiceType))
                .ToArray();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            return services.AddCacheService<TImplementation>(configure);
        }

        public IServiceCollection TryAddCacheService<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            Action<CacheServiceOptions>? configure = null)
            where TImplementation : class, ICacheService
        {
            var hasRegistration = services.Any(service => service.ServiceType == typeof(ICacheService));

            if (!hasRegistration)
            {
                return services.AddCacheService<TImplementation>(configure);
            }

            return services;
        }
    }
}
