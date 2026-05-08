---
name: nof-app-development
description: Build .NET applications using the NOF (Neat Opinionated Framework) with clean architecture, CQRS messaging, source generators, and DDD patterns. Use when the user asks to create a new NOF application, add features (entities, handlers, endpoints, caching, messaging, auth) to an existing NOF app, or references NOF abstractions like IRpcService, ICommand, Result, CacheKey, DbContext, etc.
---

# NOF Application Development

## Architecture

```text
MyApp.Domain/      domain classes, value objects, failures, in-memory event payloads
MyApp.Contract/    DTOs, RPC contracts, commands, notifications
MyApp.Application/ service implementations, handlers, state machines, cache keys
MyApp/             host program and infrastructure wiring
```

Dependency direction: `Host -> Application -> Domain`, `Host -> Contract`, `Application -> Contract`.

## Message Types

| Type | Contract | Handling |
|---|---|---|
| RPC operation | `IRpcService` method | generated nested handler base under `RpcServer<TService>` |
| Command | `ICommand` | `CommandHandler<T>` |
| Notification | `INotification` | `NotificationHandler<T>` |
| In-memory event | arbitrary payload object | `InMemoryEventHandler<T>` |

## Dispatch APIs

| Interface | Method | Use |
|---|---|---|
| Generated RPC client/service | service methods | request/response operations |
| `ICommandSender` | `SendAsync(command, ct)` | fire-and-forget |
| `INotificationPublisher` | `PublishAsync(notification, ct)` | broadcast |
| `IDeferredCommandSender` | `Send(command)` | outbox dispatch on save |
| `IDeferredNotificationPublisher` | `Publish(notification)` | outbox dispatch on save |
| `IEventPublisher` | `PublishAsync(payload, ct)` | in-scope event dispatch |

## Source Generator Surface

| Attribute / Interface | Generates |
|---|---|
| `IValueObject<T>` | equality, converters, casts, validation hooks |
| `[NewableValueObject]` | static `New()` |
| `[AutoInject]` | DI registration |
| `[HttpEndpoint]` | HTTP route metadata for RPC methods |
| `[Mappable]` | mapping registrations |
| `[Failure]` | static failure definitions |

## Decision Guide

| I want to... | Use |
|---|---|
| expose HTTP API | `IRpcService` + `[HttpEndpoint]` + `app.MapHttpEndpoint<TRpcServer>()` |
| send async work | `ICommand` + `ICommandSender` |
| publish notifications | `INotification` + `INotificationPublisher` |
| publish in-memory events | payload object + `PublishAsEvent()` or `IEventPublisher` |
| persist application data | `DbContext` / `NOFDbContext` + `SaveChangesAsync()` |
| cache data | `CacheKey<T>` + `ICacheService` |
| add JWT auth | `AddJwtAuthority(...)` and/or `AddJwtResourceServer(...)` |

## Conventions

- File-scoped namespaces, Allman braces, braces on all control-flow.
- `Optional<T>` for PATCH semantics.
- Persist application data through `DbContext` / `NOFDbContext` in application handlers.
