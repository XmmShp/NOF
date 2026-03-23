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
| `IEntity` | Marker interface for child entities | Domain |
| `IRepository<T, TKey>` | Repository abstraction (FindAsync, FindAllAsync, Add, Remove) | Domain |
| `IUnitOfWork` | Explicit update + transactional save | Application |
| `IDeferredNotificationPublisher` | Outbox-based deferred notifications | Application |
| `IDeferredCommandSender` | Outbox-based deferred commands | Application |
| `CacheKey<T>` | Typed cache key | Application |
| `IMapper` | Zero-reflection object mapper | Application |
| `Result<T>` / `Result` | Operation result with failure support | Contract |

## Source Generator Attributes & Interfaces

| Attribute / Interface | What It Generates | Layer |
|-----------|-------------------|-------|
| `IValueObject<T>` (interface) | Constructors, equality, JSON converter, explicit cast to `T`, `Of()` factory | Domain |
| `[NewableValueObject]` | Static `New()` method (SnowflakeId, requires `IValueObject<long>`) | Domain |
| `[AutoInject(Lifetime)]` | DI registration | Any |
| `[Failure(name, message, errorCode)]` | Static `Failure` instances | Domain |
| `[PublicApi]` | Marks request as public API operation (required by `[HttpEndpoint]` and `[GenerateService]`) | Contract |
| `[HttpEndpoint(HttpVerb, route)]` | HTTP endpoint mapping (requires `[PublicApi]`) | Contract |
| `[GenerateService]` | Service interface + HTTP client + `IRequestSender` client (on `partial interface`) | Contract |
| `[Mappable<TSource, TDest>]` | Auto-generated mapper registrations (on `partial static class`) | Application |

## Coding Conventions

- File-scoped namespaces (`namespace X;`)
- Allman-style braces (opening brace on new line)
- Braces required on all control-flow blocks
- `var` when type is apparent
- Private instance fields: `_camelCase`
- Constants and static fields: `PascalCase`
- All public APIs should have XML doc comments
- Use `Optional<T>` for PATCH request fields (distinguishes "not sent" from "sent as null")
- Always call `_uow.Update(entity)` after modifying an aggregate root — changes are NOT auto-detected

## Common Patterns

### Bootstrap (Program.cs)

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaults: true);
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
return Result.Fail("404", "Order not found");
```

### Object Mapping (IMapper)

Zero-reflection, explicit-only mapper. Each `MapKey(Source, Destination, Name?)` holds exactly one delegate.
No built-in mappings — all mappings must be explicitly registered (explicit > implicit).

Registration: `Add` (set/replace), `TryAdd` (skip if exists).

**Pre-build**: `services.Configure<MapperOptions>(o => o.Add<A, B>(...))`.
**Runtime**: inject `IMapper`, call `TryAdd` in constructors (no-op if key exists).

```csharp
// Simple registration
o.Add<Order, OrderDto>(o => new OrderDto(o.Id));

// With IMapper for nested mapping
o.Add<Order, OrderSummary>((o, mapper) => new OrderSummary(mapper.Map<Address, AddressDto>(o.Address)));

// Non-generic (MapFunc: (object, IMapper) → object)
o.Add(typeof(Order), typeof(OrderDto), (src, mapper) => MapOrder((Order)src));

// Named mappings
_mapper.Add<Order, OrderDto>(o => new OrderDto(o.Id), name: "summary");
var dto = _mapper.Map<Order, OrderDto>(order, name: "summary");

// TryMap (standard C# Try pattern)
if (_mapper.TryMap<Order, OrderDto>(order, out var mapped)) { /* use mapped */ }

// Fluent extensions
var dto = entity.Map.To<EntityDto>();                      // Registered mapping, fallback to cast
var dto = entity.Map.As<DerivedEntity>().To<EntityDto>();   // Change source type for lookup
var dto = entity.Map.AsRuntime.To<EntityDto>();             // Runtime type for lookup
var obj = entity.Map.To(typeof(EntityDto));                 // Non-generic
```

**Nullable fallback**: A mapping `A → T` is automatically used for `A → T?` when no direct `A → T?` registration exists.

**Source-generated mappings** — `[Mappable<TSource, TDest>]` on a `partial static class`:

```csharp
[Mappable<Order, OrderDto>]
[Mappable<Order, OrderSummary>(TwoWay = true)]
[Mappable(typeof(Config), typeof(ConfigDto))]
public static partial class Mappings;

// Generated extension method:
// options.ConfigureAutoMappings();
```

Rules: matches properties by name (case-insensitive), picks constructor with most matched params. Conversion priority:
1. Same type → direct assignment
2. Implicit conversion (including user-defined implicit operators) → direct assignment. Handles `T → Optional<T>`, `T → Result<T>` via their implicit operators.
3. User-defined explicit conversion → cast. Handles `IValueObject<T> → T` via generated `explicit operator`.
4. `Optional<T>` / `Result<T>` unwrap with strict nullable semantics: `Wrapper<T>` → `T?` ✅, `Wrapper<T>` → `T` ❌ (NOF022), `Wrapper<T?>` → anything ❌ (NOF022).
5. `IValueObject<T>` (`T : notnull`) — unwrap via explicit cast (rule 3), wrap via `VoType.Of()`. Exact underlying type only.
6. `Nullable<T>` (value types) — `Nullable<VO> → T?` and `T? → Nullable<VO>` expanded via `.HasValue`/`.Value` with recursive inner conversion.
7. `IEnumerable<T>` → collection — `[..src.Select(item => convert(item))]` collection expression with recursive element conversion.
8. Primitive conversions (string↔int, int↔enum, etc.)
9. Fallback → `mapper.Map` (NOF023 warning if pair not auto-generated)

`TwoWay = true` generates both directions. Diagnostics: NOF020 (duplicate), NOF021 (non-partial-static), NOF022 (nullable mismatch), NOF023 (unregistered fallback).

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
