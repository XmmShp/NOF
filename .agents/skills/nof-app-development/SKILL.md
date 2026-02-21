---
name: nof-app-development
description: Build .NET applications using the NOF (Neat Opinionated Framework) with clean architecture, CQRS, source generators, and DDD patterns. Use when the user asks to create a new NOF application, add features (entities, handlers, endpoints, caching, messaging, auth) to an existing NOF app, asks about NOF APIs or patterns, or references NOF abstractions like IRequest, ICommand, AggregateRoot, Result, CacheKey, etc.
---

# NOF Application Development

Build NOF applications by following this workflow:

1. **Scaffold** — Create project structure (4 layers)
2. **Model** — Define domain entities, value objects, events
3. **Contract** — Define requests, commands, notifications, DTOs
4. **Handle** — Implement handlers in the Application layer
5. **Wire** — Configure infrastructure in Program.cs

## Architecture

Four-layer clean architecture:

```
MyApp/               — Host (Program.cs, DbContext, EF migrations, appsettings.json)
MyApp.Domain/        — Entities, aggregate roots, value objects, events, repository interfaces
MyApp.Contract/      — Requests, commands, notifications, DTOs, failure definitions
MyApp.Application/   — Handlers, event handlers, state machines, cache keys
```

Dependency direction: Host → Application → Domain, Host → Contract, Application → Contract.

## CQRS Message Types

| Type | Interface | Handler | Returns |
|------|-----------|---------|---------|
| Query/Mutation | `IRequest<TResponse>` | `IRequestHandler<T, TResponse>` | `Result<TResponse>` |
| Mutation (no response) | `IRequest` | `IRequestHandler<T>` | `Result` |
| Fire-and-forget | `ICommand` | `ICommandHandler<T>` | void |
| Pub/Sub | `INotification` | `INotificationHandler<T>` | void |
| Domain Event | `IEvent` | `IEventHandler<T>` | void |

## Dispatch APIs

```csharp
IRequestSender                 → SendAsync(request, ct)       // Returns Result<T>
ICommandSender                 → SendAsync(command, ct)       // Fire-and-forget
INotificationPublisher         → PublishAsync(notification, ct) // Broadcast
IDeferredNotificationPublisher → Publish(notification)         // Outbox (on SaveChangesAsync)
IDeferredCommandSender         → Send(command)                 // Outbox (on SaveChangesAsync)
```

`IRequestSender` and `ICommandSender` accept optional `headers` and `destinationEndpointName` for cross-service messaging. `INotificationPublisher` accepts optional `headers`. `IDeferredCommandSender` accepts optional `destinationEndpointName`.

## Source Generator Attributes

| Attribute | What It Generates |
|-----------|-------------------|
| `[ValueObject<T>]` | Constructors, `Of()`, equality, JSON converter, explicit casts |
| `[NewableValueObject]` | Static `New()` method (SnowflakeId) |
| `[Snapshotable]` | Read-only snapshot record with `ToSnapshot()` |
| `[AutoInject(Lifetime)]` | DI registration (Singleton/Scoped/Transient) |
| `[ExposeToHttpEndpoint(verb, route)]` | HTTP endpoint mapping |
| `[Failure(name, message, statusCode)]` | Static `Failure` instances |
| `[Summary]` / `[EndpointDescription]` / `[Category]` | OpenAPI metadata |
| `[AllowAnonymous]` | Skip authentication |

## Decision Guide

| I want to… | Use |
|------------|-----|
| Expose an API endpoint | `IRequest` + `[ExposeToHttpEndpoint]` |
| Run background work | `ICommand` + `ICommandSender` |
| Broadcast an event | `INotification` + `INotificationPublisher` |
| React to entity changes (in-transaction) | `IEvent` + `IEventHandler` |
| Reliably publish after DB commit | `IDeferredNotificationPublisher` |
| Cache data | `CacheKey<T>` + `ICacheService` |
| Manage complex workflows | `IStateMachineDefinition<TState>` |
| Generate unique IDs | `[ValueObject<long>]` + `[NewableValueObject]` |
| Validate input values | `[ValueObject<T>]` with `Validate()` method |
| Define error types | `[Failure(name, message, code)]` |
| Register a service in DI | `[AutoInject(Lifetime)]` |

## Coding Conventions

- File-scoped namespaces (`namespace X;`), Allman-style braces, braces on all control-flow
- `var` when type is apparent; private fields: `_camelCase`; constants: `PascalCase`
- XML doc comments on public APIs
- `Optional<T>` for PATCH fields; explicit casts for value objects: `(long)orderId`
- Parameterless constructors `private`/`internal` for EF Core

## References

- **Code recipes** (bootstrap, value objects, aggregate roots, handlers, PATCH, outbox, caching, state machines): See [references/recipes.md](references/recipes.md)
- **Infrastructure setup** (EF Core, Redis, MassTransit, JWT, configuration): See [references/infrastructure.md](references/infrastructure.md)
