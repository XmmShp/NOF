---
description: Add in-process event handlers and transactional outbox dispatch to a NOF application
---

# Add In-Process Events and Transactional Messages

NOF has two separate mechanisms:

- in-process, current-scope events through `IEventPublisher` and `InMemoryEventHandler<T>`
- cross-boundary commands/notifications, optionally staged in the transactional outbox

## 1. Define and Handle an In-Process Event

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

## 2. Publish Explicitly

```csharp
await eventPublisher.PublishAsync(
    new ProjectionRebuilt("tenant-a"),
    context,
    cancellationToken);
```

Domain methods may instead call `payload.PublishAsEvent()`. At a NOF handler/event boundary, the ambient publisher carries the current `Context` into nested in-memory events.

## 3. Stage a Transactional Notification

```csharp
public sealed class CreateOrderHandler(
    IDbContext dbContext,
    INotificationPublisher publisher)
    : CommandHandler<CreateOrderCommand>
{
    public override async Task HandleAsync(
        CreateOrderCommand command,
        Context context,
        CancellationToken cancellationToken)
    {
        var order = Order.Create(EmailAddress.Of(command.CustomerEmail));
        dbContext.Set<Order>().Add(order);

        await publisher.DeferPublishAsync(
            new OrderCreatedNotification(order.Id),
            context,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

`DeferPublishAsync(...)` and `DeferSendAsync(...)` add an outbox entity to the current `IDbContext`; saving that same context commits it with application data. Immediate `PublishAsync(...)` / `SendAsync(...)` bypass the outbox.

For ordered streams, use `DeferPublishOrderedAsync(...)` or `DeferSendOrderedAsync(...)` with a stable order key. Set `completesOrderKey` only on the final message, and ensure every participating consumer sees an unbroken stream.
