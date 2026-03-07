---
description: How to add a new CQRS handler (request, command, or notification) in a NOF application
---

# Add a New CQRS Handler

> **For comprehensive guidance**, see `add-request-handler` (full HTTP endpoint workflow with OpenAPI metadata, all handler types, and dispatch patterns).

NOF uses a CQRS messaging pattern with `IRequest`, `ICommand`, and `INotification`.

## Quick Reference

### Request (query with typed response)

```csharp
// Contract
[PublicApi]
[HttpEndpoint(HttpVerb.Get, "api/orders/{id}")]
[Summary("Get order by ID")]
[Category("Orders")]
public record GetOrderRequest(long Id) : IRequest<GetOrderResponse>;
public record GetOrderResponse(long Id, string CustomerName);

// Pre-build mapping registration (Options pattern, in Program.cs or extension method)
builder.Services.Configure<MapperOptions>(o =>
    o.Add<Order, GetOrderResponse>(order => new GetOrderResponse((long)order.Id, order.CustomerName)));

// Handler
public class GetOrderHandler : IRequestHandler<GetOrderRequest, GetOrderResponse>
{
    private readonly IOrderRepository _repo;
    private readonly IMapper _mapper;

    public GetOrderHandler(IOrderRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<Result<GetOrderResponse>> HandleAsync(
        GetOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _repo.FindAsync(OrderId.Of(request.Id), cancellationToken);
        if (order is null) return Result.Fail("404", "Not found");
        
        return _mapper.Map<Order, GetOrderResponse>(order);
        // Or: return order.Map.To<GetOrderResponse>();
    }
}
```

### Request (mutation without typed response)

```csharp
// Contract
[PublicApi]
[HttpEndpoint(HttpVerb.Post, "api/orders")]
public record CreateOrderRequest(string CustomerName) : IRequest;

// Handler — returns Result (not Result<T>)
public class CreateOrderHandler : IRequestHandler<CreateOrderRequest>
{
    public async Task<Result> HandleAsync(
        CreateOrderRequest request, CancellationToken cancellationToken)
    {
        // ...
        return Result.Success();
    }
}
```

### Command (fire-and-forget)

```csharp
// Contract
public record ProcessPaymentCommand(long OrderId) : ICommand;

// Handler
public class ProcessPaymentHandler : ICommandHandler<ProcessPaymentCommand>
{
    public async Task HandleAsync(
        ProcessPaymentCommand command, CancellationToken cancellationToken)
    {
        // ...
    }
}
```

### Notification (pub/sub)

```csharp
// Contract
public record OrderShippedNotification(long OrderId) : INotification;

// Handler (multiple handlers allowed per notification)
public class SendEmailOnShipped : INotificationHandler<OrderShippedNotification>
{
    public async Task HandleAsync(
        OrderShippedNotification notification, CancellationToken cancellationToken)
    {
        // ...
    }
}
```

### Domain Event Handler

```csharp
// Domain event (raised by aggregate root)
public record OrderCreatedEvent(OrderId Id) : IEvent;

// Handler — runs within SaveChangesAsync() transaction
public class UpdateViewOnOrderCreated : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(
        OrderCreatedEvent @event, CancellationToken cancellationToken)
    {
        // ...
    }
}
```

## PATCH Request with Optional Fields

```csharp
// Contract
[PublicApi]
[HttpEndpoint(HttpVerb.Patch, "api/orders/{id}")]
public record UpdateOrderRequest : PatchRequest, IRequest
{
    public long Id { get; init; }

    public Optional<string> CustomerName
    {
        get => Get<string>();
        set => Set(value);
    }

    public Optional<string?> Notes
    {
        get => Get<string?>();
        set => Set(value);
    }
}

// Handler
public class UpdateOrderHandler : IRequestHandler<UpdateOrderRequest>
{
    public async Task<Result> HandleAsync(
        UpdateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _repo.FindAsync(OrderId.Of(request.Id), cancellationToken);
        if (order is null) return Result.Fail("404", "Not found");

        request.CustomerName.IfSome(name => order.UpdateName(name));
        request.Notes.IfSome(notes => order.UpdateNotes(notes));

        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

## Dispatch APIs

| Interface | Method | Description |
|-----------|--------|-------------|
| `IRequestSender` | `SendAsync(request, ct)` | Send request, get `Result<T>` |
| `ICommandSender` | `SendAsync(command, ct)` | Fire-and-forget command |
| `INotificationPublisher` | `PublishAsync(notification, ct)` | Broadcast notification |
| `IDeferredNotificationPublisher` | `Publish(notification)` | Outbox — published on `SaveChangesAsync()` |
| `IDeferredCommandSender` | `Send(command)` | Outbox — published on `SaveChangesAsync()` |

`IRequestSender` and `ICommandSender` accept optional `headers` and `destinationEndpointName` for cross-service messaging. `INotificationPublisher` accepts optional `headers`. `IDeferredCommandSender` accepts optional `destinationEndpointName`.

## Endpoint Metadata Attributes

| Attribute | Purpose |
|-----------|---------|
| `[PublicApi]` | Mark as public API operation |
| `[HttpEndpoint(HttpVerb, route)]` | Map to HTTP endpoint (requires `[PublicApi]`) |
| `[AllowAnonymous]` | Skip authentication |
| `[Summary("...")]` | OpenAPI summary |
| `[EndpointDescription("...")]` | OpenAPI description |
| `[Category("...")]` | OpenAPI tag/group |

## Notes

- Handlers are auto-discovered via source generators — no manual DI registration needed.
- Use `[AutoInject]` on service classes for automatic DI registration.
- Request handlers return `Result<T>` — `Result.Fail(errorCode, message)` maps to HTTP status codes.
- `PatchRequest` uses `Optional<T>` to distinguish "not sent" from "sent as null".
- Domain event handlers (`IEventHandler<T>`) run within the `SaveChangesAsync()` transaction — keep them fast.
