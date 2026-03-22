using Microsoft.Extensions.Options;
using NOF.Infrastructure;

namespace NOF.UI;

public sealed class SessionStorageCacheService(
    ISessionStorage sessionStorage,
    ICacheSerializer serializer,
    ICacheLockRetryStrategy lockRetryStrategy,
    IOptions<CacheServiceOptions> options) : BrowserStorageCacheService(sessionStorage, serializer, lockRetryStrategy, options)
{
}

