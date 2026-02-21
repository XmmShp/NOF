---
description: How to add Redis caching with typed CacheKey in a NOF application
---

# Add Redis Caching

NOF provides a typed caching abstraction via `ICacheService` and `CacheKey<T>`, backed by StackExchange.Redis.

## 1. Add NuGet Package

```bash
dotnet add package NOF.Infrastructure.StackExchangeRedis
```

## 2. Register in Program.cs

```csharp
using NOF.Infrastructure.StackExchangeRedis;

builder.AddRedisCache();
```

## 3. Configure Connection String

In `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "redis": "localhost:6379"
  }
}
```

## 4. Define Typed Cache Keys

Create cache key records in the Application project under `CacheKeys/`:

```csharp
using NOF.Application;

// Simple cache key
public record OrderCacheKey(long OrderId)
    : CacheKey<OrderDto>($"Order:{OrderId}");

// Cache key with composite key
public record UserOrdersCacheKey(string UserId, int Page)
    : CacheKey<List<OrderDto>>($"User:{UserId}:Orders:Page:{Page}");

// Cache key for a version counter
public record OrderVersionCacheKey(long OrderId)
    : CacheKey<long>($"Order:Version:{OrderId}");
```

## 5. Use ICacheService in Handlers

```csharp
using Microsoft.Extensions.Caching.Distributed;
using NOF.Application;
using NOF.Contract;

public class GetOrderHandler : IRequestHandler<GetOrderRequest, GetOrderResponse>
{
    private readonly ICacheService _cache;
    private readonly IOrderRepository _orderRepository;

    public GetOrderHandler(ICacheService cache, IOrderRepository orderRepository)
    {
        _cache = cache;
        _orderRepository = orderRepository;
    }

    public async Task<Result<GetOrderResponse>> HandleAsync(
        GetOrderRequest request, CancellationToken ct)
    {
        var cacheKey = new OrderCacheKey(request.Id);

        // Get from cache
        var cached = await _cache.GetAsync(cacheKey, ct);
        if (cached.HasValue)
        {
            return new GetOrderResponse(cached.Value);
        }

        // Fetch from database
        var order = await _orderRepository.FindAsync(OrderId.Of(request.Id), ct);
        if (order is null)
        {
            return Result.Fail(404, "Order not found");
        }

        var dto = MapToDto(order);

        // Store in cache
        await _cache.SetAsync(cacheKey, dto,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            }, ct);

        return new GetOrderResponse(dto);
    }
}
```

## 6. Get-or-Set Pattern

```csharp
var dto = await _cache.GetOrSetAsync(
    new OrderCacheKey(orderId),
    async ct =>
    {
        var order = await _orderRepository.FindAsync(OrderId.Of(orderId), ct);
        return MapToDto(order!);
    },
    new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
    },
    cancellationToken);
```

## 7. Cache Invalidation in Event Handlers

```csharp
public class InvalidateOrderCacheOnUpdate : IEventHandler<OrderUpdatedEvent>
{
    private readonly ICacheService _cache;

    public InvalidateOrderCacheOnUpdate(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task HandleAsync(OrderUpdatedEvent @event, CancellationToken ct)
    {
        await _cache.RemoveAsync(new OrderCacheKey((long)@event.Id), ct);
    }
}
```

## ICacheService API Summary

### Core Operations

| Method | Description |
|--------|-------------|
| `GetAsync<T>(key, ct)` | Returns `Optional<T>` — check `.HasValue` |
| `GetOrSetAsync<T>(key, factory, options, ct)` | Get or compute and cache (with distributed lock) |
| `SetAsync<T>(key, value, options, ct)` | Store a value |
| `RemoveAsync(key, ct)` | Remove a cached entry (via `IDistributedCache`, string key) |
| `ExistsAsync(key, ct)` | Check if key exists |

### Batch Operations

| Method | Description |
|--------|-------------|
| `GetManyAsync<T>(keys, ct)` | Batch get multiple keys |
| `SetManyAsync<T>(items, options, ct)` | Batch set multiple key-value pairs |
| `RemoveManyAsync(keys, ct)` | Batch remove, returns count removed |

### Atomic Operations

| Method | Description |
|--------|-------------|
| `IncrementAsync(key, value, ct)` | Atomically increment a numeric value |
| `DecrementAsync(key, value, ct)` | Atomically decrement a numeric value |
| `SetIfNotExistsAsync<T>(key, value, options, ct)` | Set only if key doesn't exist |
| `GetAndSetAsync<T>(key, newValue, options, ct)` | Atomically get old value and set new |
| `GetAndRemoveAsync<T>(key, ct)` | Atomically get and remove |

### TTL & Locking

| Method | Description |
|--------|-------------|
| `GetTimeToLiveAsync(key, ct)` | Get remaining TTL |
| `SetTimeToLiveAsync(key, expiration, ct)` | Set TTL on existing key |
| `AcquireLockAsync(key, expiration, ct)` | Acquire distributed lock (waits indefinitely) |
| `TryAcquireLockAsync(key, expiration, timeout, ct)` | Try to acquire lock within timeout |

All methods above accept both `string` keys and strongly-typed `CacheKey<T>` keys. `RemoveAsync` with `CacheKey<T>` is provided via C# 14 extension methods in `CacheServiceExtensions`.

## Notes

- `ICacheService` extends `IDistributedCache` — it's compatible with standard .NET caching APIs.
- Without Redis, NOF uses an in-memory cache by default (`CacheServiceRegistrationStep`). `AddRedisCache()` replaces it.
- `CacheKey<T>` is strongly typed — the generic parameter `T` ensures type safety at compile time.
- `Optional<T>` distinguishes between "not in cache" (`!HasValue`) and "cached null" (`HasValue` with `Value == null`).
- Use `ICacheServiceFactory` if you need named cache instances.
