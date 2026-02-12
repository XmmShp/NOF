# NOF.Application

Application layer package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Contains the application service abstractions: request handlers, command handlers, notification handlers, state machines, caching, and unit of work patterns. This is where your business logic orchestration lives.

## Key Abstractions

### Request Handlers

```csharp
public class GetOrderHandler : IRequestHandler<GetOrderRequest, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(
        GetOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _repository.FindAsync([request.Id], cancellationToken);
        if (order is null)
            return Result.Fail(404, "Order not found");

        return Result.Success(new OrderDto(order.Id, order.Status));
    }
}
```

### Command Handlers

```csharp
public class SendEmailHandler : CommandHandler<SendEmailCommand>
{
    public override async Task HandleAsync(
        SendEmailCommand command, CancellationToken cancellationToken)
    {
        // Fire-and-forget command processing
    }
}
```

### Notification Handlers

```csharp
public class OrderCreatedHandler : NotificationHandler<OrderCreatedNotification>
{
    public override async Task HandleAsync(
        OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        // React to domain events (pub/sub)
    }
}
```

### State Machines

Declarative, event-driven state machine with persistent context:

```csharp
public class OrderStateMachine : IStateMachineDefinition<OrderState, OrderContext>
{
    public void Build(IStateMachineBuilder<OrderState, OrderContext> builder)
    {
        builder.Correlate<OrderCreatedNotification>(n => n.OrderId.ToString());
        builder.Correlate<PaymentReceivedNotification>(n => n.OrderId.ToString());

        builder.StartWhen<OrderCreatedNotification>(
                OrderState.Pending,
                n => new OrderContext { OrderId = n.OrderId })
            .SendCommandAsync((ctx, n) => new StartProcessingCommand(n.OrderId));

        builder.On(OrderState.Pending)
            .When<PaymentReceivedNotification>()
            .Modify((ctx, n) => ctx.PaidAt = DateTime.UtcNow)
            .TransitionTo(OrderState.Completed);
    }
}
```

### Transactional Message Sending

Handler base classes provide built-in transactional outbox support — commands and notifications sent within a handler are automatically batched with the unit of work.

## Installation

```shell
dotnet add package NOF.Application
```

## License

Apache-2.0
