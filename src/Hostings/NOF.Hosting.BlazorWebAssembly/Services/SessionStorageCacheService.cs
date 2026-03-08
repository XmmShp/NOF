using Microsoft.Extensions.Options;
using NOF.Infrastructure.Abstraction;

namespace NOF.Hosting.BlazorWebAssembly;

public sealed class SessionStorageCacheService(
    ISessionStorage sessionStorage,
    ICacheSerializer serializer,
    ICacheLockRetryStrategy lockRetryStrategy,
    IOptions<CacheServiceOptions> options) : BrowserStorageCacheService(sessionStorage, serializer, lockRetryStrategy, options)
{
}
