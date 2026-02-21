---
description: How to add a state machine with persistent context in a NOF application
---

# Add a State Machine

NOF provides a declarative, event-driven state machine builder with persistent context via EF Core.

## 1. Define States

```csharp
public enum OrderState
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}
```

## 2. Define Notifications (Triggers)

State machine transitions are driven by `INotification` messages:

```csharp
using NOF.Contract;

public record OrderPlacedNotification(string OrderId) : INotification;
public record PaymentReceivedNotification(string OrderId) : INotification;
public record OrderShippedNotification(string OrderId) : INotification;
public record DeliveryConfirmedNotification(string OrderId) : INotification;
public record OrderCancelledNotification(string OrderId, string Reason) : INotification;
```

## 3. Define Commands (Optional Side Effects)

```csharp
using NOF.Contract;

public record StartProcessingCommand(string OrderId) : ICommand;
```

## 4. Implement the State Machine Definition

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NOF.Application;

public class OrderStateMachine : IStateMachineDefinition<OrderState>
{
    // Correlation key — maps notifications to state machine instances
    private static string OrderKey(string orderId) => $"Order-{orderId}";

    public void Build(IStateMachineBuilder<OrderState> builder)
    {
        // Register correlation extractors for each notification type
        builder.Correlate<OrderPlacedNotification>(n => OrderKey(n.OrderId));
        builder.Correlate<PaymentReceivedNotification>(n => OrderKey(n.OrderId));
        builder.Correlate<OrderShippedNotification>(n => OrderKey(n.OrderId));
        builder.Correlate<DeliveryConfirmedNotification>(n => OrderKey(n.OrderId));
        builder.Correlate<OrderCancelledNotification>(n => OrderKey(n.OrderId));

        // Initial state — triggered by OrderPlacedNotification
        builder.StartWhen<OrderPlacedNotification>(OrderState.Pending)
            .SendCommandAsync(n => new StartProcessingCommand(n.OrderId));

        // Pending → Processing (on payment received)
        builder.On(OrderState.Pending)
            .When<PaymentReceivedNotification>()
            .Execute((n, sp) =>
            {
                sp.GetRequiredService<ILogger<OrderStateMachine>>()
                    .LogInformation("Payment received for order {OrderId}", n.OrderId);
            })
            .TransitionTo(OrderState.Processing);

        // Processing → Shipped
        builder.On(OrderState.Processing)
            .When<OrderShippedNotification>()
            .TransitionTo(OrderState.Shipped);

        // Shipped → Delivered
        builder.On(OrderState.Shipped)
            .When<DeliveryConfirmedNotification>()
            .TransitionTo(OrderState.Delivered);

        // Any state → Cancelled (before shipping)
        builder.On(OrderState.Pending)
            .When<OrderCancelledNotification>()
            .TransitionTo(OrderState.Cancelled);

        builder.On(OrderState.Processing)
            .When<OrderCancelledNotification>()
            .TransitionTo(OrderState.Cancelled);
    }
}
```

## 5. Implement Command Handler (Side Effect)

```csharp
using NOF.Application;

public class StartProcessingHandler : ICommandHandler<StartProcessingCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IDeferredNotificationPublisher _publisher;

    public StartProcessingHandler(IUnitOfWork uow, IDeferredNotificationPublisher publisher)
    {
        _uow = uow;
        _publisher = publisher;
    }

    public async Task HandleAsync(StartProcessingCommand command, CancellationToken ct)
    {
        // Do processing work...
        await Task.Delay(1000, ct);

        // Publish result via transactional outbox
        _publisher.Publish(new PaymentReceivedNotification(command.OrderId));
        await _uow.SaveChangesAsync(ct);
    }
}
```

## 6. Trigger the State Machine

Publish the initial notification to start the state machine:

```csharp
await _notificationPublisher.PublishAsync(
    new OrderPlacedNotification(orderId), cancellationToken);
```

## Fluent API Reference

| Method | Description |
|--------|-------------|
| `builder.Correlate<T>(selector)` | Register correlation ID extractor |
| `builder.StartWhen<T>(initialState)` | Define initial state trigger |
| `builder.On(state).When<T>()` | Define transition from state on notification |
| `.Execute(action)` | Run synchronous action during transition |
| `.ExecuteAsync(asyncAction)` | Run async action during transition |
| `.SendCommandAsync(factory)` | Send a command during transition |
| `.PublishNotificationAsync(factory)` | Publish a notification during transition |
| `.TransitionTo(state)` | Set the target state |

## Notes

- State machine context is persisted via `IStateMachineContextRepository` (EF Core by default).
- The correlation ID uniquely identifies a state machine instance — all notifications for the same instance must return the same correlation ID.
- `StartWhen<T>` creates a new state machine instance; subsequent `On(state).When<T>()` rules advance existing instances.
- Actions in `.Execute()` receive `(notification, IServiceProvider)` — resolve any service you need.
- Use `IDeferredNotificationPublisher` inside command handlers to ensure notifications are only published after the transaction commits.
