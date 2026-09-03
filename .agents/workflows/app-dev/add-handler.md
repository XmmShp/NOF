---
description: Add NOF RPC, command, notification, or in-memory event handlers with the current Context-aware signatures
---

# Add a Handler or RPC Server

NOF has four handler shapes. A concrete class may inherit only one command, notification, or in-memory event handler base (`NOF001`).

## RPC Pattern

```csharp
[TransportOverHttp(HttpRpcStyle.ControllerRpc)]
public interface IOrderService : IRpcService
{
    [HttpEndpoint(HttpVerb.Get, "api/orders/get")]
    Result<GetOrderResponse> GetOrder(GetOrderRequest request);
}

public partial class OrderService : RpcServer<IOrderService>;

public sealed class GetOrder : OrderService.GetOrder
{
    public override Task<Result<GetOrderResponse>> HandleAsync(
        GetOrderRequest request,
        Context context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new GetOrderResponse(request.Id, "demo")));
    }
}
```

Register the server explicitly:

```csharp
builder.AddApplicationPart(typeof(OrderService).Assembly);
builder.AddRpcServer<OrderService>();
```

## Command Pattern

```csharp
public sealed record RebuildCacheCommand(string TenantId);

public sealed class RebuildCacheHandler : CommandHandler<RebuildCacheCommand>
{
    public override Task HandleAsync(
        RebuildCacheCommand command,
        Context context,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

## Notification Pattern

```csharp
public sealed record OrderCreatedNotification(string OrderId);

public sealed class OrderCreatedHandler : NotificationHandler<OrderCreatedNotification>
{
    public override Task HandleAsync(
        OrderCreatedNotification notification,
        Context context,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

## In-Memory Event Pattern

```csharp
public sealed record ProjectionRebuilt(string TenantId);

public sealed class ProjectionRebuiltHandler : InMemoryEventHandler<ProjectionRebuilt>
{
    public override Task HandleAsync(
        ProjectionRebuilt @event,
        Context context,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

Command, notification, and event handlers are discovered through generated assembly initializers. Ensure the containing assembly is passed to `AddApplicationPart(...)`.
