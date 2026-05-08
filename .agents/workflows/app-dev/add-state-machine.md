---
description: How to add a state machine with persistent context in a NOF application
---

# Add a State Machine

NOF provides a declarative, event-driven state machine builder with persistent context support.

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

## 2. Define Notifications

```csharp
public record OrderPlacedNotification(string OrderId);
public record PaymentReceivedNotification(string OrderId);
public record OrderShippedNotification(string OrderId);
```

## 3. Implement the State Machine Definition

```csharp
public class OrderStateMachine : IStateMachineDefinition<OrderState>
{
    private static string OrderKey(string orderId) => $"Order-{orderId}";

    public void Build(IStateMachineBuilder<OrderState> builder)
    {
        builder.Correlate<OrderPlacedNotification>(n => OrderKey(n.OrderId));
        builder.Correlate<PaymentReceivedNotification>(n => OrderKey(n.OrderId));
        builder.Correlate<OrderShippedNotification>(n => OrderKey(n.OrderId));

        builder.StartWhen<OrderPlacedNotification>(OrderState.Pending)
            .SendCommandAsync(n => new StartProcessingCommand(n.OrderId));

        builder.On(OrderState.Pending)
            .When<PaymentReceivedNotification>()
            .TransitionTo(OrderState.Processing);

        builder.On(OrderState.Processing)
            .When<OrderShippedNotification>()
            .TransitionTo(OrderState.Shipped);
    }
}
```

## Notes

- State machine notifications are ordinary payload types handled through `NotificationHandler<T>` registrations.
- Register one `Correlate<TNotification>(...)` rule for every notification type the state machine handles.
- `StartWhen<TNotification>(...)` defines the first transition.
- Later transitions are configured with `On(state).When<TNotification>()`.
