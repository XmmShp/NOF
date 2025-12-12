using Microsoft.Extensions.Caching.Distributed;
using NOF.Sample.Application.CacheKeys;

namespace NOF.Sample.Application.EventHandlers;

public class UpdateCacheOnConfigNodeUpdated : IEventHandler<ConfigNodeUpdatedEvent>
{
    private readonly IDistributedCache _cache;

    public UpdateCacheOnConfigNodeUpdated(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task HandleAsync(ConfigNodeUpdatedEvent @event, CancellationToken cancellationToken)
    {
        // 1. Clear view repository caches (Ensure next read gets fresh data)
        await _cache.RemoveAsync(new ConfigNodeByIdCacheKey(@event.Id), cancellationToken);
        await _cache.RemoveAsync(new ConfigNodeByNameCacheKey(@event.Name), cancellationToken);

        // 2. Update Global Incremental Timestamp (Version) for this node
        var version = DateTime.UtcNow.Ticks;
        await _cache.SetAsync(
            new ConfigNodeVersionCacheKey(@event.Id),
            version,
            options: new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) },
            token: cancellationToken);
    }
}
