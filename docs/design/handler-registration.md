# Handler Registration: First-Class Citizens

## Handlers in NOF

In NOF, handlers are the primary unit of business logic. Every user-facing operation - processing a command, responding to a request, reacting to a domain event, broadcasting a notification - is implemented as a handler. They are first-class citizens: the framework discovers them at compile time, registers them into DI with precise keys, and provides infrastructure (pipelines, transports, middleware) that operates on them uniformly.

There are three handler kinds, split into two families based on their dispatch semantics:

**Point-to-point** (one message - one handler):
- `ICommandHandler<TCommand>` - fire-and-forget commands

**Multicast** (one message - many handlers):
- `IEventHandler<TEvent>` - domain events (in-process, within the aggregate boundary)
- `INotificationHandler<TNotification>` - cross-boundary notifications

This distinction shapes every design decision in the registration system.

## Source-Generated Discovery

Handlers are never registered manually. A Roslyn incremental source generator (`HandlerRegistrationGenerator`) scans the compilation - both source code in the current project and metadata from prefix-matching referenced assemblies - for concrete, non-abstract, non-generic classes implementing any of the three handler interfaces.

For each discovered handler, the generator emits an `AddAllHandlers` extension method on `IServiceCollection` that:

1. Records handler metadata into typed singleton collections (`CommandHandlerInfos`, `EventHandlerInfos`, etc.)
2. Registers the handler as a keyed scoped service
3. Populates the `EndpointNameRegistry` with compile-time-resolved endpoint names

The generated code uses fully-qualified names (`global::`) throughout to avoid namespace collisions.

## Keyed Service Registration

All handlers are registered as **keyed scoped services** in the root DI container. The key is a strongly-typed composite: `XxxHandlerKey.Of(typeof(TMessage))`. Each handler kind has its own key type (`CommandHandlerKey`, `EventHandlerKey`, `NotificationHandlerKey`), which prevents accidental cross-resolution between different handler families.

The registration strategy differs by dispatch semantics:

### Point-to-Point Handlers (Command)

Command handlers have a one-to-one relationship between message type and handler. They are registered as the **concrete type** only:

```csharp
services.AddKeyedScoped<MyCommandHandler>(CommandHandlerKey.Of(typeof(MyCommand)));
```

Consumers resolve the concrete type directly:

```csharp
var handler = sp.GetRequiredKeyedService<MyCommandHandler>(CommandHandlerKey.Of(typeof(MyCommand)));
```

This ensures that only code with knowledge of the concrete handler type can resolve it. Other services cannot accidentally obtain a handler through a broad interface query.

### Multicast Handlers (Event, Notification)

Multicast handlers have a one-to-many relationship: multiple handlers can subscribe to the same message type. They receive **dual registration** - both the concrete type and a factory-based interface delegation:

```csharp
// 1. Concrete registration (for RabbitMQ adapters and direct resolution)
services.AddKeyedScoped<MyEventHandler>(EventHandlerKey.Of(typeof(MyEvent)));

// 2. Interface factory (for in-process multicast dispatch)
services.AddKeyedScoped<IEventHandler>(
    EventHandlerKey.Of(typeof(MyEvent)),
    (sp, key) => sp.GetRequiredKeyedService<MyEventHandler>(key));
```

The factory delegates to the concrete registration, ensuring a single instance per scope. The non-generic `IEventHandler` and `INotificationHandler` interfaces use **Default Interface Methods (DIM)** to bridge the generic and non-generic worlds:

```csharp
public interface IEventHandler<in TEvent> : IEventHandler where TEvent : class, IEvent
{
    Task IEventHandler.HandleAsync(IEvent @event, CancellationToken ct)
        => HandleAsync((TEvent)@event, ct);

    Task HandleAsync(TEvent @event, CancellationToken ct);
}
```

This allows the in-process publisher to resolve all handlers for a given event type through the non-generic interface and dispatch without knowing the concrete types:

```csharp
foreach (var handler in sp.GetKeyedServices<IEventHandler>(EventHandlerKey.Of(eventType)))
{
    await handler.HandleAsync(@event, ct);
}
```

## Handler Info Records

Each handler kind has its own typed info record:

| Record | Fields |
|--------|--------|
| `CommandHandlerInfo` | `HandlerType`, `CommandType` |
| `EventHandlerInfo` | `HandlerType`, `EventType` |
| `NotificationHandlerInfo` | `HandlerType`, `NotificationType` |

These are collected into typed singleton `HashSet` containers (`CommandHandlerInfos`, `EventHandlerInfos`, etc.) registered via `GetOrAddSingleton`. Infrastructure components - such as the RabbitMQ integration - read these collections at startup to wire up transport-level consumers.

## Endpoint Name Resolution

Point-to-point handlers need routable endpoint names for message transport (e.g., RabbitMQ queues). The `EndpointNameRegistry` (a `ConcurrentDictionary<Type, string>` singleton) stores the mapping from handler/message types to endpoint names.

Endpoint names are resolved at compile time by the source generator:

1. `[EndpointName("...")]` attribute on the handler or message type - explicit override
2. For handlers implementing exactly one point-to-point interface - the message type's endpoint name
3. Fallback - `BuildSafeTypeName`: a deterministic, namespace-safe string derived from the fully-qualified type name

Event-only handlers do not receive endpoint names (they are dispatched in-process, not routed through queues).

The `HandlerSelector` fluent API returned by `AddAllHandlers` allows runtime overrides:

```csharp
services.AddAllHandlers()
    .SetEndpointName<MyCommand>("custom-command-queue");
```

## RabbitMQ Integration

The RabbitMQ integration reads the non-event handler info collections and creates typed adapter consumers:

- `RabbitMQCommandHandlerAdapter<THandler, TCommand>`
- `RabbitMQRequestHandlerAdapter<THandler, TRequest>`
- `RabbitMQRequestHandlerAdapter<THandler, TRequest, TResponse>`
- `RabbitMQNotificationHandlerAdapter<THandler, TNotification>`

Each adapter injects `IServiceProvider` and resolves the handler via `GetRequiredKeyedService<THandler>(Key.Of(messageType))` at consume time - **not** through constructor injection. This means:

- Handlers are never registered as plain scoped services; they are only accessible through their keyed registrations
- No other service can accidentally resolve a handler without the correct key
- The handler's scoped lifetime is tied to the RabbitMQ consume context

## Design Principles

1. **Compile-time discovery** - No reflection at runtime. The source generator does all the work.
2. **Keyed isolation** - Handlers are only resolvable through their typed keys. No ambient service pollution.
3. **Semantic split** - Point-to-point (concrete only) vs. multicast (concrete + interface factory) registrations match the dispatch semantics exactly.
4. **DIM for type erasure** - `IEventHandler` and `INotificationHandler` use Default Interface Methods to bridge generic handlers to non-generic multicast dispatch.
5. **Single source of truth** - `EndpointNameRegistry` and typed `HandlerInfos` singletons are populated once at startup and consumed by all infrastructure.



