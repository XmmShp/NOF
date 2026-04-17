using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Application.Extension.Redis;
using NOF.Hosting;
using StackExchange.Redis;

namespace NOF.Infrastructure.StackExchangeRedis;

public static class NOFInfrastructureExtensions
{
    /// <param name="builder">The <see cref="INOFAppBuilder"/>.</param>
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Replaces the default in-memory cache with a Redis-based cache service using StackExchange.Redis.
        /// </summary>
        /// <param name="configureConnectionOptions">Configures StackExchange.Redis connection options.</param>
        /// <param name="configureCacheOptions">Optional action to configure cache service options.</param>
        /// <returns>The <see cref="INOFAppBuilder"/> so that additional calls can be chained.</returns>
        public INOFAppBuilder AddRedisCache(Action<ConfigurationOptions> configureConnectionOptions, Action<CacheServiceOptions>? configureCacheOptions = null)
        {
            ArgumentNullException.ThrowIfNull(configureConnectionOptions);

            if (configureCacheOptions is not null)
            {
                builder.Services.Configure(configureCacheOptions);
            }

            builder.Services.ReplaceOrAddSingleton<IConnectionMultiplexer>(sp =>
            {
                return ConnectionMultiplexer.Connect(string.Empty, configureConnectionOptions);
            });

            builder.Services.ReplaceOrAddScoped<ICacheService, RedisCacheService>();
            builder.Services.ReplaceOrAddScoped(sp => (IRedisCacheService)sp.GetRequiredService<ICacheService>());

            return builder;
        }
    }
}
