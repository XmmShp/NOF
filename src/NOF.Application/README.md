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

Zero-reflection, manually configured object mapper. Each mapping delegate returns `Optional<T>` — multiple delegates per key are supported (last-added evaluated first, first `HasValue` wins).

**Registration** — `Add` (append), `TryAdd` (skip if key exists), `ReplaceOrAdd` (clear + set):

```csharp
// Pre-build (Options pattern)
builder.Services.Configure<MapperOptions>(o =>
    o.Add<ConfigFile, ConfigFileDto>(f =>
        new ConfigFileDto((string)f.Name, (string)f.Content)));

// Runtime — TryAdd is safe in constructors (no-op if key already registered)
_mapper.TryAdd<ConfigNode, ConfigNodeDto>(node => new ConfigNodeDto(...));

// Non-generic
_mapper.Add(typeof(Order), typeof(OrderDto), src => Optional.Of<object?>(MapOrder((Order)src)));
```

**Named mappings** — multiple names per type pair:

```csharp
_mapper.Add<Order, OrderDto>(o => new OrderDto(o.Id), name: "summary");
_mapper.Add<Order, OrderDto>(o => new OrderDto(o.Id, o.Details), name: "full");
var dto = _mapper.Map<Order, OrderDto>(order, name: "full");
```

**Fluent extension syntax:**

```csharp
var dto = entity.Map.To<EntityDto>();                   // Registered mapping, fallback to cast
var dto = entity.Map.To<EntityDto>(name: "v2");         // Named mapping
var obj = entity.Map.To(typeof(EntityDto));              // Non-generic
var dto = entity.Map.As<DerivedEntity>().To<EntityDto>(); // Change source type for lookup
var dto = entity.Map.AsRuntime.To<EntityDto>();           // Use runtime type for lookup
```

**Built-in mappings** (automatic, no registration required):

| Conversion | Example |
|------------|---------|
| `IValueObject<T>` → `T` | `OrderId` → `long` (via `GetUnderlyingValue()`) |
| `IValueObject<T>` → chain | `OrderId` → `string` (underlying → ToString) |
| `Result<T>` → `T` | Extract `Value` if `IsSuccess` |
| `Optional<T>` → `T` | Extract `Value` if `HasValue` |
| Numeric ↔ numeric | `int` → `long`, `decimal` → `int`, etc. |
| Enum ↔ numeric | `DayOfWeek` → `int`, `int` → `DayOfWeek` |
| Any `T` → `string` | Calls `ToString()` (lowest priority) |
| `A` → `T?` | Falls back to `A` → `T` mapping |

User-registered mappings always take priority over built-ins. Built-ins only apply to unnamed (default) mappings.

## Installation

```shell
dotnet add package NOF.Application
```

## License

Apache-2.0
