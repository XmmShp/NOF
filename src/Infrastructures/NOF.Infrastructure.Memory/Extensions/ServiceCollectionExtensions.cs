using Microsoft.Extensions.DependencyInjection;

namespace NOF;

// ReSharper disable once InconsistentNaming
public static partial class __NOF_Infrastructure_Memory_Extensions__
{
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds a memory-based cache service for development and testing.
        /// </summary>
        /// <param name="configureOptions">Optional action to configure the cache service options.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public IServiceCollection AddMemoryCache(Action<CacheServiceOptions>? configureOptions = null)
        {
            var options = new CacheServiceOptions();
            configureOptions?.Invoke(options);

            return services.AddCacheService<MemoryCacheService>((sp, opt) =>
            {
                var serializer = opt.GetSerializer(sp);
                var lockRetryStrategy = opt.GetLockRetryStrategy(sp);
                return new MemoryCacheService(serializer, lockRetryStrategy, opt);
            }, options);
        }
    }
}
