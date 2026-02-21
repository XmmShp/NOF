---
description: How to add domain event handlers and use the transactional outbox in a NOF application
---

# Add Domain Event Handlers & Transactional Outbox

NOF dispatches domain events raised by aggregate roots when `IUnitOfWork.SaveChangesAsync()` is called. Events can update read models, invalidate caches, or trigger side effects.

## 1. Define a Domain Event

In the Domain project:
```csharp
using NOF.Domain;

public record OrderCreatedEvent(OrderId Id, string CustomerName) : IEvent;
public record OrderUpdatedEvent(OrderId Id) : IEvent;
```

## 2. Raise Events from Aggregate Root

```csharp
public class Order : AggregateRoot
{
    public static Order Create(string customerName)
    {
        var order = new Order { Id = OrderId.New(), CustomerName = customerName };
        order.AddEvent(new OrderCreatedEvent(order.Id, customerName));
        return order;
    }

    public void UpdateName(string newName)
    {
        CustomerName = newName;
        AddEvent(new OrderUpdatedEvent(Id));
    }
}
```

## 3. Implement Domain Event Handlers

In the Application project under `EventHandlers/`:

```csharp
using NOF.Application;

// Update a read model
public class UpdateOrderViewOnCreated : IEventHandler<OrderCreatedEvent>
{
    private readonly IOrderViewRepository _viewRepo;

    public UpdateOrderViewOnCreated(IOrderViewRepository viewRepo)
    {
        _viewRepo = viewRepo;
    }

    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        await _viewRepo.CreateViewAsync(@event.Id, @event.CustomerName, ct);
    }
}

// Invalidate cache
public class InvalidateCacheOnOrderUpdated : IEventHandler<OrderUpdatedEvent>
{
    private readonly ICacheService _cache;

    public InvalidateCacheOnOrderUpdated(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task HandleAsync(OrderUpdatedEvent @event, CancellationToken ct)
    {
        await _cache.RemoveAsync(new OrderCacheKey((long)@event.Id), ct);
    }
}
```

## 4. Domain Events vs Notifications

| Feature | Domain Event (`IEvent`) | Notification (`INotification`) |
|---------|------------------------|-------------------------------|
| Scope | In-process only | In-process + distributed (MassTransit) |
| Dispatch | Automatic on `SaveChangesAsync()` | Manual via `INotificationPublisher` |
| Handler | `IEventHandler<T>` | `INotificationHandler<T>` |
| Transactional | Same transaction as aggregate | Via transactional outbox |

## 5. Transactional Outbox

The outbox ensures notifications are only published after the database transaction commits — preventing message loss.

### Using IDeferredNotificationPublisher

```csharp
public class CreateOrderHandler : IRequestHandler<CreateOrderRequest>
{
    private readonly IOrderRepository _orderRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDeferredNotificationPublisher _publisher;

    public CreateOrderHandler(
        IOrderRepository orderRepo,
        IUnitOfWork uow,
        IDeferredNotificationPublisher publisher)
    {
        _orderRepo = orderRepo;
        _uow = uow;
        _publisher = publisher;
    }

    public async Task<Result> HandleAsync(CreateOrderRequest request, CancellationToken ct)
    {
        var order = Order.Create(request.CustomerName);
        _orderRepo.Add(order);

        // Deferred — written to outbox table in the same transaction
        _publisher.Publish(new OrderCreatedNotification((long)order.Id));

        // Domain events (IEvent) + outbox messages are all committed atomically
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

### How It Works

1. `_publisher.Publish(notification)` — adds the notification to the outbox context (not yet persisted).
2. `_uow.SaveChangesAsync()` — in a single transaction:
   - Persists the aggregate root changes
   - Dispatches domain events (`IEvent`) to `IEventHandler<T>` handlers
   - Writes outbox messages to the `OutboxMessage` table
3. A background service picks up outbox messages and publishes them via MassTransit.
4. The inbox (`InboxMessage` table) ensures idempotent processing on the consumer side.

## 6. Multiple Handlers for Same Event

You can register multiple handlers for the same domain event:

```csharp
// Handler 1: Update read model
public class UpdateViewOnOrderCreated : IEventHandler<OrderCreatedEvent> { ... }

// Handler 2: Invalidate cache
public class InvalidateCacheOnOrderCreated : IEventHandler<OrderCreatedEvent> { ... }

// Handler 3: Send welcome email
public class SendWelcomeOnOrderCreated : IEventHandler<OrderCreatedEvent> { ... }
```

All handlers execute within the same transaction scope as `SaveChangesAsync()`.

## Notes

- Domain event handlers (`IEventHandler<T>`) run synchronously within the `SaveChangesAsync()` call — keep them fast.
- For long-running side effects, use `IDeferredNotificationPublisher` to publish a notification that will be processed asynchronously.
- The outbox/inbox tables are automatically configured by `NOFDbContext.OnModelCreating()`.
- Background cleanup services (`InboxCleanupBackgroundService`, `OutboxCleanupBackgroundService`) are registered automatically.
