---
name: nof-app-development
description: Build .NET applications using the NOF (Neat Opinionated Framework) with clean architecture, CQRS messaging, source generators, and DDD patterns. Use when the user asks to create a new NOF application, add features (entities, handlers, endpoints, caching, messaging, auth) to an existing NOF app, or references NOF abstractions like `IRpcService`, `CommandHandler<T>`, `NotificationHandler<T>`, `Result`, `CacheKey`, or `DbContext`.
---

# NOF Application Development

## Architecture

```text
MyApp.Domain/      domain classes, value objects, failures, in-memory event payloads
MyApp.Contract/    DTOs, RPC contracts, request/response models
MyApp.Application/ service implementations, handlers, state machines, cache keys
MyApp/             host program and infrastructure wiring
```

Dependency direction: `Host -> Application -> Domain`, `Host -> Contract`, `Application -> Contract`.

## Message Types

| Type | Contract | Handling |
|---|---|---|
| RPC operation | `IRpcService` method | generated nested handler base under `RpcServer<TService>` |
| Command | plain payload object | `CommandHandler<T>` |
| Notification | plain payload object | `NotificationHandler<T>` |
| In-memory event | arbitrary payload object | `InMemoryEventHandler<T>` |

## Dispatch APIs

| Interface | Method | Use |
|---|---|---|
| Generated RPC client/service | service methods | request/response operations |
| `ICommandSender` | `SendAsync(command, ct)` | immediate command dispatch |
| `ICommandSender` | `DeferSend(command)` | outbox dispatch on save |
| `INotificationPublisher` | `PublishAsync(notification, ct)` | immediate broadcast |
| `INotificationPublisher` | `DeferPublish(notification)` | outbox dispatch on save |
| `IEventPublisher` | `PublishAsync(payload, context, ct)` | in-scope event dispatch with execution-context propagation |

`PublishAsEvent()` combines the publisher bound once by the current dependency injection scope's daemon service with the `Context` bound at the current handler or event-dispatch boundary. Domain methods do not need to accept `Context` solely to forward it to an in-memory event.

## Source Generator Surface

| Attribute / Interface | Generates |
|---|---|
| `IValueObject<T>` | equality, converters, casts, validation hooks; construct through `Of(...)`/generated factories, never `default` or parameterless `new()` (`NOF018`) |
| `[NewableValueObject]` | static `New()` and `New(IIdGenerator)` |
| `[ValueObjectLength(maximumLength, MinimumLength = ...)]` | string range validation and persistence maximum-length metadata |
| `[AutoInject]` | DI registration |
| `[HttpEndpoint]` | HTTP route metadata for RPC methods |
| `[TransportOverMemory]` | in-process-only RPC transport without external endpoint mapping |
| `[Mappable]` | mapping registrations |
| `[Failure]` | static failure definitions |

## Decision Guide

| I want to... | Use |
|---|---|
| expose HTTP API | `IRpcService` + `[TransportOverHttp]` + `builder.AddRpcServer<TRpcServer>()` |
| keep an RPC service in process | `IRpcService` + `[TransportOverMemory]` + generated local client |
| send async work | payload object + `ICommandSender` |
| publish notifications | payload object + `INotificationPublisher` |
| publish in-memory events | payload object + `PublishAsEvent()` / `PublishAsEvent(publisher)` / `IEventPublisher` |
| persist application data | `DbContext` / `NOFDbContext` + `SaveChangesAsync()` |
| cache data | `CacheKey<T>` + `ICacheService` |
| add JWT auth | `AddAuthenticationAuthority(...)` and/or `AddAuthenticationResourceServer(...)` |

## Conventions

- File-scoped namespaces, Allman braces, braces on all control-flow.
- `Optional<T>` for PATCH semantics.
- Persist application data through `DbContext` / `NOFDbContext` in application handlers.
- Mapping is expression-based; apply `ProjectTo<TDestination>()` only after filtering, ordering, and paging. Its `IQueryable` receiver resolves the source key from `ElementType` and uses the current async-flow-scoped `Mapper`.
- Treat `NOF025` as an advisory signal that server-side query shaping occurs after `ProjectTo`; move that shaping before projection unless the warning is a documented conservative-analysis edge case.
- Treat `Mapper`, `IdGenerator`, and `EventPublisher` ambient APIs as convenience only; keep their explicit paths available when that improves clarity or testability.
