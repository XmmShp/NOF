# NOF.Application

Application layer package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Contains the application service abstractions used to implement NOF applications: RPC servers, request handlers, command handlers, notification handlers, state machines, mapping, caching, and unit of work patterns.

## Key Abstractions

### RPC Servers

RPC contracts are declared on `IRpcService` interfaces in the contract layer. Application implementations use `RpcServer<TService>`:

```csharp
public partial class OrderService : RpcServer<IOrderService>;

public class GetOrder : OrderService.GetOrder
{
    public override async Task<Result<OrderDto>> HandleAsync(GetOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _repository.FindAsync(request.Id, cancellationToken);
        if (order is null)
        {
            return Result.Fail("404", "Order not found");
        }

        return new OrderDto(order.Id, order.Status);
    }
}
```

### Command Handlers

```csharp
public class SendEmailHandler : CommandHandler<SendEmailCommand>
{
    public override Task HandleAsync(SendEmailCommand command, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

### Notification Handlers

```csharp
public class OrderCreatedHandler : NotificationHandler<OrderCreatedNotification>
{
    public override Task HandleAsync(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

### State Machines

Declarative, event-driven state machines:

```csharp
public class OrderStateMachine : IStateMachineDefinition<OrderState>
{
    public void Build(IStateMachineBuilder<OrderState> builder)
    {
        builder.Correlate<OrderCreatedNotification>(n => n.OrderId.ToString());
        builder.Correlate<PaymentReceivedNotification>(n => n.OrderId.ToString());

        builder.StartWhen<OrderCreatedNotification>(OrderState.Pending)
            .SendCommandAsync(n => new StartProcessingCommand(n.OrderId));

        builder.On(OrderState.Pending)
            .When<PaymentReceivedNotification>()
            .TransitionTo(OrderState.Completed);
    }
}
```

### Transactional Message Sending

Handlers can use `IDeferredCommandSender` and `IDeferredNotificationPublisher` for outbox-backed dispatch coordinated with the unit of work.

### Object Mapping (`IMapper`)

NOF uses an explicit mapper with optional source-generated registrations via `[Mappable]`:

```csharp
[Mappable<Order, OrderDto>]
[Mappable<Order, OrderSummary>(TwoWay = true)]
public static partial class Mappings;
```

Register generated mappings at startup:

```csharp
builder.Services.Configure<MapperOptions>(options => options.ConfigureAutoMappings());
```

## Installation

```shell
dotnet add package NOF.Application
```

`ICacheService` also implements `IDistributedCache`, so standard distributed cache consumers can resolve either abstraction from NOF cache registrations.

## License

Apache-2.0
