using Microsoft.Extensions.Options;
using NOF.Infrastructure;

namespace NOF.UI;

public sealed class LocalStorageCacheService(
    ILocalStorage localStorage,
    ICacheSerializer serializer,
    ICacheLockRetryStrategy lockRetryStrategy,
    IOptions<CacheServiceOptions> options) : BrowserStorageCacheService(localStorage, serializer, lockRetryStrategy, options)
{
}

