using Microsoft.Extensions.Options;
using NOF.Infrastructure.Abstraction;

namespace NOF.Hosting.BlazorWebAssembly;

public sealed class LocalStorageCacheService(
    ILocalStorage localStorage,
    ICacheSerializer serializer,
    ICacheLockRetryStrategy lockRetryStrategy,
    IOptions<CacheServiceOptions> options) : BrowserStorageCacheService(localStorage, serializer, lockRetryStrategy, options)
{
}
