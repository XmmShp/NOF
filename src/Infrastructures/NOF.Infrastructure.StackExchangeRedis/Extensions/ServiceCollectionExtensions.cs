using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Infrastructure;
using NOF.Infrastructure.StackExchangeRedis;
using StackExchange.Redis;

namespace NOF.Hosting;

public static partial class NOFInfrastructureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRedisBackplane(ConfigurationOptions connectionOptions)
        {
            ArgumentNullException.ThrowIfNull(connectionOptions);

            services.ReplaceOrAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionOptions));
            services.ReplaceOrAddSingleton<IBackplane, RedisBackplane>();
            return services;
        }

        public IServiceCollection AddRedisBackplane(string configuration, Action<ConfigurationOptions>? configureConnectionOptions = null)
        {
            if (configureConnectionOptions is not null)
            {
                services.ReplaceOrAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configuration, configureConnectionOptions));
            }
            else
            {
                services.ReplaceOrAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configuration));
            }

            services.ReplaceOrAddSingleton<IBackplane, RedisBackplane>();
            return services;
        }

        public IServiceCollection AddRedisCache(ConfigurationOptions connectionOptions, Action<CacheServiceOptions>? configureCacheOptions = null)
        {
            ArgumentNullException.ThrowIfNull(connectionOptions);

            if (configureCacheOptions is not null)
            {
                services.Configure(configureCacheOptions);
            }

            services.ReplaceOrAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionOptions));
            services.ReplaceOrAddScoped<ICacheServiceRider, RedisCacheServiceRider>();
            return services;
        }

        public IServiceCollection AddRedisCache(string configuration, Action<ConfigurationOptions>? configureConnectionOptions = null, Action<CacheServiceOptions>? configureCacheOptions = null)
        {
            if (configureCacheOptions is not null)
            {
                services.Configure(configureCacheOptions);
            }

            if (configureConnectionOptions is not null)
            {
                services.ReplaceOrAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configuration, configureConnectionOptions));
            }
            else
            {
                services.ReplaceOrAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configuration));
            }

            services.ReplaceOrAddScoped<ICacheServiceRider, RedisCacheServiceRider>();
            return services;
        }
    }
}
