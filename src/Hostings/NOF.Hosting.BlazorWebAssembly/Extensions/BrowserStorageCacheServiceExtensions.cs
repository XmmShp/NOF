using Microsoft.Extensions.DependencyInjection;
using NOF.Infrastructure;

namespace NOF.Hosting.BlazorWebAssembly;

public static class NOFHostingBlazorWebAssemblyExtensions
{
    extension(IServiceCollection services)
    {
        public ICacheServiceBuilder AddLocalStorageCacheService(string? name = null, Action<CacheServiceOptions>? configure = null)
        {
            return services.AddCacheService<LocalStorageCacheService>(name ?? ICacheServiceFactory.DefaultName, configure);
        }

        public ICacheServiceBuilder AddSessionStorageCacheService(string? name = null, Action<CacheServiceOptions>? configure = null)
        {
            return services.AddCacheService<SessionStorageCacheService>(name ?? ICacheServiceFactory.DefaultName, configure);
        }
    }
}
