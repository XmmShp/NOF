---
trigger: always_on
---

# NOF Application Development Rules

> **Audience**: Human developers AND AI coding assistants building applications that USE the NOF framework.
> If you are contributing to the NOF framework itself, see `rules/nof-dev.md` instead.

## Architecture

NOF applications follow a clean architecture with four layers:

```
MyApp.Domain/        — Entities, aggregate roots, value objects, domain events, repository interfaces
MyApp.Contract/      — Requests, commands, notifications, DTOs, failure definitions
MyApp.Application/   — Handlers, event handlers, state machines, cache keys
MyApp/               — Host project (Program.cs, DbContext, EF migrations, appsettings.json)
```

**Dependency direction**: Host → Application → Domain, Host → Contract, Application → Contract.

## Key Abstractions

| Abstraction | Purpose | Layer |
|-------------|---------|-------|
| `IRequest<TResponse>` / `IRequest` | Query or mutation message | Contract |
| `ICommand` | Fire-and-forget message | Contract |
| `INotification` | Pub/sub broadcast message | Contract |
| `IEvent` | Domain event (in-process, transactional) | Domain |
| `IRequestHandler<T, TResponse>` | Handles a request | Application |
| `ICommandHandler<T>` | Handles a command | Application |
| `INotificationHandler<T>` | Handles a notification | Application |
| `IEventHandler<T>` | Handles a domain event | Application |
| `AggregateRoot` | DDD aggregate root base class | Domain |
| `IRepository<T, TKey>` | Repository abstraction | Domain |
| `IUnitOfWork` | Transactional unit of work | Application |
| `IDeferredNotificationPublisher` | Outbox-based deferred notifications | Application |
| `IDeferredCommandSender` | Outbox-based deferred commands | Application |
| `CacheKey<T>` | Typed cache key | Application |
| `IMapper` | Zero-reflection object mapper | Application |
| `Result<T>` / `Result` | Operation result with failure support | Contract |

## Source Generator Attributes

| Attribute | What It Generates | Layer |
|-----------|-------------------|-------|
| `[ValueObject<T>]` | Constructors, equality, JSON converter, casts | Domain |
| `[NewableValueObject]` | Static `New()` method (SnowflakeId) | Domain |
| `[AutoInject(Lifetime)]` | DI registration | Any |
| `[ExposeToHttpEndpoint(verb, route)]` | HTTP endpoint mapping | Contract |
| `[Failure(name, message, statusCode)]` | Static `Failure` instances | Contract/Domain |
| `[Snapshotable]` | Read-only snapshot record | Domain |

## Coding Conventions

- File-scoped namespaces (`namespace X;`)
- Allman-style braces (opening brace on new line)
- Braces required on all control-flow blocks
- `var` when type is apparent
- Private instance fields: `_camelCase`
- Constants and static fields: `PascalCase`
- All public APIs should have XML doc comments
- Use `Optional<T>` for PATCH request fields (distinguishes "not sent" from "sent as null")

## Common Patterns

### Bootstrap (Program.cs)

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);
builder.Services.AddMyAppAutoInjectServices();  // Source-generated
builder.Services.AddAllHandlers();               // Source-generated
builder.AddRedisCache();
builder.AddJwtAuthority().AddJwksRequestHandler();
builder.AddJwtAuthorization();
builder.AddMassTransit().UseRabbitMQ();
builder.AddEFCore<AppDbContext>().AutoMigrate().UsePostgreSQL();
var app = await builder.BuildAsync();
app.MapAllHttpEndpoints();
await app.RunAsync();
```

### Result Pattern

```csharp
// Success
return Result.Success();
return new GetOrderResponse(id, name);  // implicit conversion to Result<T>

// Failure
return Result.Fail(OrderFailures.OrderNotFound);
return Result.Fail(404, "Order not found");
```

### Object Mapping (IMapper)

Zero-reflection, manually configured mapper. Register mappings in constructor (first-wins, no GC pressure):

```csharp
public class ConfigNodeViewRepository : IConfigNodeViewRepository
{
    private readonly IMapper _mapper;

    public ConfigNodeViewRepository(IMapper mapper)
    {
        _mapper = mapper;
        
        // Register in constructor - safe to call multiple times (first-wins)
        _mapper.CreateMap<ConfigFileSnapshot, ConfigFileDto>(f => 
            new ConfigFileDto((string)f.Name, (string)f.Content));
        
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

**Fluent syntax:**

```csharp
var dto = entity.Map.To<EntityDto>();        // Registered mapping, fallback to cast
var baseEntity = derived.Map.As<BaseEntity>(); // Inheritance cast only
```

**MapOrCreate** - register if missing, then map:

```csharp
var dto = _mapper.MapOrCreate(entity, e => new EntityDto(e.Id, e.Name));
```

### Dispatch APIs

| Interface | Method | Description |
|-----------|--------|-------------|
| `IRequestSender` | `SendAsync(request, ct)` | Send request, get `Result<T>` |
| `ICommandSender` | `SendAsync(command, ct)` | Fire-and-forget command |
| `INotificationPublisher` | `PublishAsync(notification, ct)` | Broadcast notification |
| `IDeferredNotificationPublisher` | `Publish(notification)` | Outbox — published on `SaveChangesAsync()` |
| `IDeferredCommandSender` | `Send(command)` | Outbox — published on `SaveChangesAsync()` |

## Available Workflows

See `.agents/workflows/app-dev/` for step-by-step guides:

- `scaffold-nof-app.md` — Create a new NOF application from scratch
- `add-domain-entity.md` — Add entities, aggregate roots, value objects, domain events
- `add-request-handler.md` — Add CQRS request handlers with HTTP endpoints
- `add-handler.md` — Quick reference for all handler types
- `add-domain-event-handler.md` — Domain event handlers and transactional outbox
- `add-efcore-database.md` — Set up EF Core with PostgreSQL
- `add-jwt-auth.md` — JWT authentication and authorization
- `add-masstransit-messaging.md` — Distributed messaging with MassTransit + RabbitMQ
- `add-redis-caching.md` — Redis caching with typed cache keys
- `add-state-machine.md` — Declarative state machines with persistent context
