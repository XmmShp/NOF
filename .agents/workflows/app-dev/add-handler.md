---
description: Quick guide for NOF handler and RPC server implementation patterns
---

# Add Handler or RPC Server Implementation

NOF currently has:

- RPC server implementations (`RpcServer<TService>`)
- command handlers (`CommandHandler<T>`)
- notification handlers (`NotificationHandler<T>`)
- in-memory event handlers (`InMemoryEventHandler<T>`)

## RPC Pattern

```csharp
public interface IOrderService : IRpcService
{
    [HttpEndpoint(HttpVerb.Get, "api/orders/get")]
    Result<GetOrderResponse> GetOrder(GetOrderRequest request);
}

public partial class OrderService : RpcServer<IOrderService>;

public sealed class GetOrder : OrderService.GetOrder
{
    public override Task<Result<GetOrderResponse>> HandleAsync(GetOrderRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new GetOrderResponse(request.Id, "demo")));
    }
}
```

## Command Pattern

```csharp
public record RebuildCacheCommand(string TenantId);

public sealed class RebuildCacheHandler : CommandHandler<RebuildCacheCommand>
{
    public override Task HandleAsync(RebuildCacheCommand command, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

## Notification Pattern

```csharp
public record OrderCreatedNotification(string OrderId);

public sealed class OrderCreatedHandler : NotificationHandler<OrderCreatedNotification>
{
    public override Task HandleAsync(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

## In-Memory Event Pattern

```csharp
public record ProjectionRebuilt(string TenantId);

public sealed class ProjectionRebuiltHandler : InMemoryEventHandler<ProjectionRebuilt>
{
    public override Task HandleAsync(ProjectionRebuilt @event, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```
