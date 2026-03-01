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

Zero-reflection, manually configured object mapper. All mappings must be explicitly registered — no reflection or conventions are used.

```csharp
public class ConfigNodeViewRepository : IConfigNodeViewRepository
{
    private readonly IMapper _mapper;

    public ConfigNodeViewRepository(IMapper mapper)
    {
        _mapper = mapper;

        // Register mappings in constructor (first-wins, no GC pressure on duplicates)
        _mapper.CreateMap<ConfigNode, ConfigNodeDto>(node => new ConfigNodeDto(
            (long)node.Id,
            node.ParentId.HasValue ? (long)node.ParentId.Value : null,
            (string)node.Name,
            node.ActiveFileName.HasValue ? (string)node.ActiveFileName.Value : null,
            node.ConfigFiles.Select(f => _mapper.Map<ConfigFileSnapshot, ConfigFileDto>(f)).ToList()
        ));
    }

    public async Task<ConfigNodeDto?> GetByIdAsync(ConfigNodeId id)
    {
        var node = await _repository.FindAsync(id);
        return node is null ? null : _mapper.Map<ConfigNode, ConfigNodeDto>(node);
    }
}
```

**Fluent extension syntax:**

```csharp
// .To<T>() - uses registered mapping, falls back to cast
var dto = domainEntity.Map.To<EntityDto>();

// .As<T>() - inheritance/interface cast only (no mapping lookup)
var baseEntity = derivedEntity.Map.As<BaseEntity>();
```

**MapOrCreate** - ensures mapping exists without caring if it was already registered:

```csharp
var dto = _mapper.MapOrCreate(entity, e => new EntityDto(e.Id, e.Name));
```

## Installation

```shell
dotnet add package NOF.Application
```

## License

Apache-2.0
