---
name: nof-app-development
description: Build .NET applications using the NOF (Neat Opinionated Framework) with clean architecture, CQRS messaging, source generators, and DDD patterns. Use when the user asks to create a new NOF application, add features (entities, handlers, endpoints, caching, messaging, auth) to an existing NOF app, or references NOF abstractions like IRpcService, ICommand, AggregateRoot, Result, CacheKey, etc.
---

# NOF Application Development

## Architecture

```text
MyApp.Domain/      entities, value objects, events, repositories
MyApp.Contract/    DTOs, RPC contracts, commands, notifications
MyApp.Application/ service implementations, handlers, state machines, cache keys
MyApp/             host program and infrastructure wiring
```

Dependency direction: `Host -> Application -> Domain`, `Host -> Contract`, `Application -> Contract`.

## Message Types

| Type | Contract | Handling |
|---|---|---|
| RPC operation | `IRpcService` method | generated service implementation base class |
| Command | `ICommand` | `ICommandHandler<T>` |
| Notification | `INotification` | `INotificationHandler<T>` |
| Domain event | `IEvent` | `IEventHandler<T>` |

## Dispatch APIs

| Interface | Method | Use |
|---|---|---|
| Generated RPC client/service | service methods | request/response operations |
| `ICommandSender` | `SendAsync(command, ct)` | fire-and-forget |
| `INotificationPublisher` | `PublishAsync(notification, ct)` | broadcast |
| `IDeferredCommandSender` | `Send(command)` | outbox dispatch on save |
| `IDeferredNotificationPublisher` | `Publish(notification)` | outbox dispatch on save |

## Source Generator Surface

| Attribute / Interface | Generates |
|---|---|
| `IValueObject<T>` | equality, converters, casts, validation hooks |
| `[NewableValueObject]` | static `New()` |
| `[AutoInject]` | DI registration |
| `[GenerateService]` | service contract + clients |
| `[HttpEndpoint]` | HTTP route metadata for RPC methods |
| `[PublicApi]` | public API marker |
| `[Mappable]` | mapping registrations |
| `[Failure]` | static failure definitions |

## Decision Guide

| I want to... | Use |
|---|---|
| expose HTTP API | `IRpcService` + `[GenerateService]` + `[HttpEndpoint]` |
| send async work | `ICommand` + `ICommandSender` |
| publish events | `INotification` + `INotificationPublisher` |
| persist aggregate changes | `_uow.Update(entity)` + `_uow.SaveChangesAsync()` |
| cache data | `CacheKey<T>` + `ICacheService` |
| add JWT auth | `AddJwtAuthority(...)` and/or `AddJwtResourceServer(...)` |

## Conventions

- File-scoped namespaces, Allman braces, braces on all control-flow.
- `Optional<T>` for PATCH semantics.
- Explicitly call `_uow.Update(entity)` after aggregate mutation.
