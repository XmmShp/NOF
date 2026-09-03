---
description: Replace NOF's in-memory cache rider with Redis while retaining typed CacheKey APIs
---

# Add Redis Caching

NOF exposes typed caching through `ICacheService` and `CacheKey<T>`. `NOF.Infrastructure.StackExchangeRedis` replaces the storage rider while keeping the application API unchanged.

## 1. Add the Package to the Host

```bash
dotnet add package NOF.Infrastructure.StackExchangeRedis
```

## 2. Register Redis

```csharp
builder.Services.AddRedisCache(builder.Configuration.GetConnectionString("redis")
    ?? throw new InvalidOperationException("Connection string 'redis' not found."));
```

The overloads accept a StackExchange.Redis `ConfigurationOptions` or a connection string, plus optional connection/cache configuration delegates.

## 3. Configure the Connection

```json
{
  "ConnectionStrings": {
    "redis": "localhost:6379"
  }
}
```

## 4. Define Typed Keys

```csharp
using NOF.Application;

public sealed record OrderCacheKey(long OrderId)
    : CacheKey<OrderDto>($"Order:{OrderId}");
```

## 5. Use the Cache from an Application Handler

```csharp
public sealed class GetOrder(IDbContext dbContext, ICacheService cache)
    : OrderService.GetOrder
{
    public override async Task<Result<OrderDto>> HandleAsync(
        GetOrderRequest request,
        Context context,
        CancellationToken cancellationToken)
    {
        var key = new OrderCacheKey(request.Id);
        var cached = await cache.GetAsync(key, cancellationToken);
        if (cached.HasValue)
        {
            return Result.Success(cached.Value);
        }

        var order = await dbContext.Set<Order>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == request.Id, cancellationToken);
        if (order is null)
        {
            return Result.Fail("404", "Order not found");
        }

        var dto = new OrderDto(order.Id, order.Status);
        await cache.SetAsync(key, dto, cancellationToken: cancellationToken);
        return Result.Success(dto);
    }
}
```

`ICacheService` also implements `IDistributedCache` and supports multi-key operations, atomic counters, TTL operations, and distributed locks. Cache keys are tenant-prefixed by default; use `IgnoreQueryFilters()` only for deliberate cross-tenant/host access.

Use `builder.Services.AddRedisBackplane(...)` separately when `IBackplane` should use Redis as well.
