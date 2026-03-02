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

### Object Mapping (IMapper)

Zero-reflection, explicit-only object mapper. Each `MapKey(Source, Destination, Name?)` holds exactly one delegate.
No built-in mappings are provided — all mappings must be explicitly registered (explicit > implicit).

**Registration** — `Add` (set/replace), `TryAdd` (skip if key exists):

```csharp
// Pre-build (Options pattern)
builder.Services.Configure<MapperOptions>(o =>
    o.Add<ConfigFile, ConfigFileDto>(f =>
        new ConfigFileDto((string)f.Name, (string)f.Content)));

// With IMapper for nested mapping
o.Add<Order, OrderSummary>((o, mapper) => new OrderSummary(mapper.Map<Address, AddressDto>(o.Address)));

// Runtime — TryAdd is safe in constructors (no-op if key already registered)
_mapper.TryAdd<ConfigNode, ConfigNodeDto>(node => new ConfigNodeDto(...));

// Non-generic (MapFunc: (object, IMapper) → object)
_mapper.Add(typeof(Order), typeof(OrderDto), (src, mapper) => MapOrder((Order)src));
```

**Named mappings** — multiple names per type pair:

```csharp
_mapper.Add<Order, OrderDto>(o => new OrderDto(o.Id), name: "summary");
_mapper.Add<Order, OrderDto>(o => new OrderDto(o.Id, o.Details), name: "full");
var dto = _mapper.Map<Order, OrderDto>(order, name: "full");
```

**TryMap** — standard C# `Try` pattern with `out` parameter:

```csharp
if (_mapper.TryMap<Order, OrderDto>(order, out var dto))
{
    // dto is the mapped value
}
```

**Fluent extension syntax:**

```csharp
var dto = entity.Map.To<EntityDto>();                   // Registered mapping, fallback to cast
var dto = entity.Map.To<EntityDto>(name: "v2");         // Named mapping
var obj = entity.Map.To(typeof(EntityDto));              // Non-generic
var dto = entity.Map.As<DerivedEntity>().To<EntityDto>(); // Change source type for lookup
var dto = entity.Map.AsRuntime.To<EntityDto>();           // Use runtime type for lookup
```

**Nullable fallback**: A mapping `A → T` is automatically used for `A → T?` when no direct `A → T?` registration exists.

## Installation

```shell
dotnet add package NOF.Application
```

## License

Apache-2.0
