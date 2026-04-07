---
description: Quick guide for NOF handler/service implementation patterns
---

# Add Handler or Service Implementation

NOF currently has:
- RPC service implementations (generated from `IRpcService` contracts)
- command handlers (`ICommandHandler<T>`)
- notification handlers (`INotificationHandler<T>`)
- domain event handlers (`IEventHandler<T>`)

## RPC Pattern

```csharp
[GenerateService]
public partial interface IOrderService : IRpcService
{
    [PublicApi]
    [HttpEndpoint(HttpVerb.Get, "api/orders/{id}")]
    Task<Result<GetOrderResponse>> GetOrderAsync(GetOrderRequest request, CancellationToken cancellationToken = default);
}

public sealed class GetOrder : OrderService.GetOrder
{
    public override Task<Result<GetOrderResponse>> GetOrderAsync(GetOrderRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new GetOrderResponse(request.Id, "demo")));
    }
}
```

## Command Pattern

```csharp
public record RebuildCacheCommand(string TenantId) : ICommand;

public sealed class RebuildCacheHandler : ICommandHandler<RebuildCacheCommand>
{
    public Task HandleAsync(RebuildCacheCommand command, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

## Notification Pattern

```csharp
public record OrderCreatedNotification(string OrderId) : INotification;

public sealed class OrderCreatedHandler : INotificationHandler<OrderCreatedNotification>
{
    public Task HandleAsync(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```
