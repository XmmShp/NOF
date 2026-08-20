---
description: How to add in-process event handlers and transactional outbox behavior in a NOF application
---

# Add In-Process Event Handlers and Transactional Outbox

NOF supports two complementary patterns:

- in-process events via `InMemoryEventHandler<T>` and `IEventPublisher`
- transactional outbox dispatch via `INotificationPublisher.DeferPublish(...)` and `ICommandSender.DeferSend(...)`

## 1. Define an In-Process Event

```csharp
public record ProjectionRebuilt(string TenantId);
```

## 2. Implement an Event Handler

```csharp
public sealed class ProjectionRebuiltHandler : InMemoryEventHandler<ProjectionRebuilt>
{
    public override Task HandleAsync(ProjectionRebuilt @event, Context context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

## 3. Publish the Event in Scope

```csharp
await _eventPublisher.PublishAsync(new ProjectionRebuilt("tenant-a"), context, cancellationToken);
```

## 4. Use the Transactional Outbox for Cross-Boundary Work

```csharp
public sealed class CreateOrderHandler : CommandHandler<CreateOrderCommand>
{
    private readonly DbContext _dbContext;
    private readonly INotificationPublisher _publisher;

    public CreateOrderHandler(DbContext dbContext, INotificationPublisher publisher)
    {
        _dbContext = dbContext;
        _publisher = publisher;
    }

    public override async Task HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var order = Order.Create(EmailAddress.Of(command.CustomerEmail));
        _dbContext.Set<Order>().Add(order);
        _publisher.DeferPublish(new OrderCreatedNotification(order.Id));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

## Notes

- Use `InMemoryEventHandler<T>` for same-scope, in-process reactions. Pass the current `Context` to preserve execution metadata downstream.
- Domain code can continue to call `PublishAsEvent()` without accepting `Context`; NOF binds the current handler context to the ambient publisher and preserves it across secondary in-memory events.
- Use deferred notifications or commands when the work should flow through the outbox and optional transport integrations such as RabbitMQ.
- Persist data through `DbContext` / `NOFDbContext` in the application layer.
