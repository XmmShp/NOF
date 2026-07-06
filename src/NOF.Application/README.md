# NOF.Application

Application layer package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Contains the application service abstractions used to implement NOF applications: RPC servers, request handlers, command handlers, notification handlers, state machines, mapping, caching, and persistence contracts.

`AddNOFApplication()` registers the package-local application defaults, including mapper/state-machine registries, the ambient mapper convenience API support, and the package-local Domain defaults.

Commands and notifications are plain payload types. Handler discovery comes from the `CommandHandler<T>` and `NotificationHandler<T>` base classes rather than marker interfaces on the message types.

## Key Abstractions

### RPC Servers

RPC contracts are declared on `IRpcService` interfaces in the contract layer. Application implementations use `RpcServer<TService>`:

```csharp
public partial class OrderService : RpcServer<IOrderService>;

using NOF.Application;

public sealed class GetOrder : OrderService.GetOrder
{
    private readonly IDbContext _dbContext;

    public GetOrder(IDbContext dbContext)
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

Streaming RPC handlers use the same generated nested `RpcHandler<TRequest, StreamingResult<T>>` model:

```csharp
public partial class OrderService : RpcServer<IOrderService>;

public sealed class Watch : OrderService.Watch
{
    public override Task<StreamingResult<OrderEvent>> HandleAsync(WatchOrdersRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(StreamingResult.Success(Stream()));

        async IAsyncEnumerable<OrderEvent> Stream()
        {
            yield return new OrderEvent(Guid.NewGuid(), "Created");
            await Task.Delay(1000, cancellationToken);
            yield return new OrderEvent(Guid.NewGuid(), "Shipped");
        }
    }
}
```

The contract surface for the same method remains `StreamingResult<OrderEvent> Watch(WatchOrdersRequest request);`.

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

### Persistence Abstractions

Application code should depend on `IDbContext`, `IDbSet<TEntity>`, and async query helpers under `NOF.Application` rather than a concrete ORM type.

```csharp
using NOF.Application;

var exists = await _dbContext.Set<Order>()
    .AsNoTracking()
    .AnyAsync(order => order.Id == request.Id, cancellationToken);
```

The async query surface is exposed through `IAsyncQueryable<T>` plus extension methods such as `AnyAsync`, `CountAsync`, `FirstOrDefaultAsync`, `SingleAsync`, `ToListAsync`, `SumAsync`, and `AverageAsync`. Concrete infrastructure adapters decide how those terminal operations are executed.

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

For package-local defaults:

```csharp
services.AddNOFApplication();
```

This registers the default `IMapper`, mapping registries, and scoped ambient mapper activation.
Custom hosts still need the equivalent of `ResolveDaemonServices()` if they want to use the ambient `source.Map` convenience API.

`AddNOFApplication()` already includes `AddNOFDomain()`:

```csharp
services.AddNOFApplication();
```

If you want to override the default `IIdGenerator`, register your own implementation explicitly:

```csharp
services.AddNOFApplication();
services.AddSingleton<IIdGenerator, MyIdGenerator>();
```

## Installation

```shell
dotnet add package NOF.Application
```

`ICacheService` also implements `IDistributedCache`, so standard distributed cache consumers can resolve either abstraction from NOF cache registrations.

## License

Apache-2.0
