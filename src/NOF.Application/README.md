# NOF.Application

Application layer package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Contains the application service abstractions used to implement NOF applications: RPC servers, request handlers, command handlers, notification handlers, state machines, mapping, and caching.

Commands and notifications are plain payload types. Handler discovery comes from the `CommandHandler<T>` and `NotificationHandler<T>` base classes rather than marker interfaces on the message types.

## Key Abstractions

### RPC Servers

RPC contracts are declared on `IRpcService` interfaces in the contract layer. Application implementations use `RpcServer<TService>`:

```csharp
public partial class OrderService : RpcServer<IOrderService>;

public sealed class GetOrder : OrderService.GetOrder
{
    private readonly DbContext _dbContext;

    public GetOrder(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task<Result<OrderDto>> HandleAsync(GetOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Set<Order>()
            .FirstOrDefaultAsync(entity => entity.Id == request.Id, cancellationToken);
        if (order is null)
        {
            return Result.Fail("404", "Order not found");
        }

        return Result.Success(new OrderDto(order.Id, order.Status));
    }
}
```

### Command Handlers

```csharp
public record SendEmailCommand(string To, string Subject, string Body);

public sealed class SendEmailHandler : CommandHandler<SendEmailCommand>
{
    public override Task HandleAsync(SendEmailCommand command, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

### Notification Handlers

```csharp
public record OrderCreatedNotification(Guid OrderId);

public sealed class OrderCreatedHandler : NotificationHandler<OrderCreatedNotification>
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
public sealed class OrderStateMachine : IStateMachineDefinition<OrderState>
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

Use `ICommandSender` and `INotificationPublisher` for both immediate and deferred dispatch:

```csharp
_notificationPublisher.DeferPublish(new OrderCreatedNotification(order.Id));
_commandSender.DeferSend(new SendEmailCommand(order.Email, "Created", "Order created."));
await _dbContext.SaveChangesAsync(cancellationToken);
```

### Object Mapping (`IMapper`)

NOF uses an explicit mapper with optional source-generated registrations via `[Mappable]`:

```csharp
[Mappable<Order, OrderDto>]
[Mappable<Order, OrderSummary>(TwoWay = true)]
public static partial class Mappings;
```

The generator writes `MapperRegistration` entries into `Registry.MapperRegistry`.
Those mappings become available once the assembly is added via `AddApplicationPart(...)`.
Convenience APIs can use the ambient mapper via `source.Map`, while explicit code can use `source.MapWith(mapper)`.

## Installation

```shell
dotnet add package NOF.Application
```

`ICacheService` also implements `IDistributedCache`, so standard distributed cache consumers can resolve either abstraction from NOF cache registrations.

## License

Apache-2.0
