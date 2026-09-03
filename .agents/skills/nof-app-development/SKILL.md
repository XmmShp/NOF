---
name: nof-app-development
description: Build .NET applications using the NOF (Neat Opinionated Framework) with clean architecture, RPC/CQRS messaging, provider-neutral persistence, source generators, caching, and OAuth/OIDC. Use when creating or changing a NOF app or when working with abstractions such as IRpcService, RpcServer<T>, CommandHandler<T>, NotificationHandler<T>, IDbContext, IRepository<T>, Result, or CacheKey<T>.
---

# NOF Application Development

Read `../../rules/app-dev.md` first. Use `references/recipes.md` for application code shapes and `references/infrastructure.md` for provider and authentication setup.

## Architecture

```text
MyApp.Domain/      domain types, value objects, failures, in-memory event payloads
MyApp.Contract/    RPC contracts, requests/responses, commands, notifications, DTOs
MyApp.Application/ RPC servers and handlers, message/event handlers, mappings, cache keys
MyApp/             host and concrete persistence/transport configuration
```

Application code should remain provider-neutral by using `IDbContext` and `IRepository<T>`; concrete EF Core or NHibernate types belong in the host.

## Message and RPC Types

| Concern | Declaration | Handler / registration |
|---|---|---|
| RPC | `IRpcService` + exactly one transport attribute | generated nested base under `RpcServer<TService>`; register with `AddRpcServer<TServer>()` |
| Command | plain payload | `CommandHandler<T>` |
| Notification | plain payload | `NotificationHandler<T>` |
| In-memory event | arbitrary payload | `InMemoryEventHandler<T>` |

All generated RPC handlers, command handlers, notification handlers, and in-memory event handlers receive `Context` plus `CancellationToken`.

## Dispatch APIs

| Interface | Method | Use |
|---|---|---|
| Generated RPC client | `OperationAsync(request, context, ct)` | transport-selected request/response or stream |
| `ICommandSender` | `SendAsync(command, context, ct)` | immediate command dispatch |
| `ICommandSender` | `DeferSendAsync(command, context, ct)` | stage a command in the current outbox |
| `ICommandSender` | `DeferSendOrderedAsync(command, orderKey, context, ..., ct)` | stage ordered command delivery |
| `INotificationPublisher` | `PublishAsync(notification, context, ct)` | immediate fan-out |
| `INotificationPublisher` | `DeferPublishAsync(notification, context, ct)` | stage a notification in the current outbox |
| `INotificationPublisher` | `DeferPublishOrderedAsync(notification, orderKey, context, ..., ct)` | stage ordered notification delivery |
| `IEventPublisher` | `PublishAsync(payload, context, ct)` | current-scope in-memory event dispatch |

Save the same `IDbContext` after a deferred dispatch to commit the application changes and outbox record together.

`PublishAsEvent()` uses the ambient publisher and current handler context. Prefer an injected `IEventPublisher` and its explicit async API when asynchronous control or test visibility matters.

## Source Generator Surface

| Attribute / interface | Behavior |
|---|---|
| `IValueObject<T>` | generates validated `Of(...)`, explicit primitive cast, equality, JSON conversion, and initialization checks |
| `[NewableValueObject]` | for `IValueObject<long>` only; generates `New()` and `New(IIdGenerator)` |
| `[ValueObjectLength(max, MinimumLength = ...)]` | validates string length and supplies persistence maximum-length metadata |
| `[AutoInject(ServiceLifetime...)]` | emits DI descriptors through an assembly initializer |
| `[Failure(...)]` | generates static `Failure` members |
| `[Mappable<TSource, TDestination>]` | generates expression mapping registration on a `partial static` class |
| `[TransportOverHttp(style, prefix)]` | selects controller RPC or JSON-RPC HTTP transport |
| `[TransportOverMemory]` | limits a contract to generated in-process clients |
| `[HttpEndpoint]` | declares controller-style HTTP verb and operation route |

## Decision Guide

| Goal | Use |
|---|---|
| expose controller-style HTTP RPC | `[TransportOverHttp(HttpRpcStyle.ControllerRpc)]`, method `[HttpEndpoint]`, `AddRpcServer<T>()` |
| expose JSON-RPC | `[TransportOverHttp(HttpRpcStyle.JsonRpc, routePrefix)]`, no method `[HttpEndpoint]` |
| keep RPC in process | `[TransportOverMemory]` and generated local client |
| persist application data | `IDbContext` / `IRepository<T>`; choose EF Core or NHibernate in the host |
| cache data | `CacheKey<T>` + `ICacheService`; optionally replace the memory rider with Redis |
| send cross-boundary work | `ICommandSender` / `INotificationPublisher`, passing the current `Context` |
| add an OAuth/OIDC server | `AddOidcServer(...)` |
| validate access tokens | `services.AddAuthenticationResourceServer(...)` |

## Conventions

- Add assemblies containing handlers, mappings, or `[AutoInject]` services through `AddApplicationPart(...)`.
- Register every RPC server explicitly through `AddRpcServer<TServer>()`; applicable endpoints are mapped during `BuildAsync()`.
- Build request contracts with one reference-type request and a non-task `IResult` return.
- Keep persistence-provider APIs out of the application layer.
- Apply filtering, ordering, and paging before `ProjectTo<TDestination>()`.
- Keep explicit paths available when ambient `Mapper`, `IdGenerator`, or `EventPublisher` conveniences make dependencies unclear.
