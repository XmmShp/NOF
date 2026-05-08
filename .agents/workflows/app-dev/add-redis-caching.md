---
description: How to add Redis caching with typed CacheKey in a NOF application
---

# Add Redis Caching

NOF provides typed caching via `ICacheService` and `CacheKey<T>`, with Redis support from `NOF.Infrastructure.StackExchangeRedis`.

## 1. Add NuGet Package

```bash
dotnet add package NOF.Infrastructure.StackExchangeRedis
```

## 2. Register in Program.cs

```csharp
using NOF.Infrastructure.StackExchangeRedis;

builder.AddRedisCache(builder.Configuration.GetConnectionString("redis"));
```

## 3. Configure Connection String

```json
{
  "ConnectionStrings": {
    "redis": "localhost:6379"
  }
}
```

## 4. Define Typed Cache Keys

```csharp
using NOF.Application;

public record OrderCacheKey(long OrderId)
    : CacheKey<OrderDto>($"Order:{OrderId}");
```

## 5. Use `ICacheService` in Handlers

```csharp
public class GetOrder : OrderService.GetOrder
{
    private readonly ICacheService _cache;
    private readonly IOrderRepository _orderRepository;

    public GetOrder(ICacheService cache, IOrderRepository orderRepository)
    {
        _cache = cache;
        _orderRepository = orderRepository;
    }

    public override async Task<Result<OrderDto>> HandleAsync(GetOrderRequest request, CancellationToken cancellationToken)
    {
        var cacheKey = new OrderCacheKey(request.Id);
        var cached = await _cache.GetAsync(cacheKey, cancellationToken: cancellationToken);
        if (cached.HasValue)
        {
            return cached.Value;
        }

        var order = await _orderRepository.FindAsync(OrderId.Of(request.Id), cancellationToken);
        if (order is null)
        {
            return Result.Fail("404", "Order not found");
        }

        var dto = new OrderDto(request.Id, "demo");
        await _cache.SetAsync(cacheKey, dto, cancellationToken: cancellationToken);
        return dto;
    }
}
```

## Notes

- `ICacheService` also satisfies `IDistributedCache`.
- `AddRedisCache(...)` replaces the default in-memory cache registration.
- Use typed `CacheKey<T>` records rather than ad-hoc string keys when possible.
