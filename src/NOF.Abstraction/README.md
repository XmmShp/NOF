# NOF.Abstraction

Cross-cutting abstractions package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides shared contracts and annotations intended for use across layers:

- `[AutoInject]` via `Microsoft.Extensions.DependencyInjection`
- `InMemoryEventHandler<TEvent>` / `IEventPublisher`
- `AddNOFAbstraction()` for package-local runtime registration
- ambient event publishing helpers via `EventPublisher` and `PublishAsEvent(...)`

## In-Memory Events

NOF provides a scoped in-memory event mechanism for invoking distributed handlers within the same dependency injection scope.

- Any non-null `object` can be used as an in-memory event payload
- `InMemoryEventHandler<TEvent>` handles that payload
- `IEventPublisher` dispatches the event to all handlers resolved from the current scope

This mechanism can be used by domain aggregates, application services, or any other in-scope collaboration that should remain in-process.

For convenience, NOF also exposes an ambient publisher facade:

- `payload.PublishAsEvent()` uses the ambient `IEventPublisher` for the current async flow
- `payload.PublishAsEvent(publisher)` is the explicit alternative when ambient scope is not desired
- `AddNOFAbstraction()` registers the scoped ambient activation service

The ambient API is a convenience layer. The explicit `IEventPublisher` dependency remains the primary runtime contract.

## Installation

```shell
dotnet add package NOF.Abstraction
```

## Runtime Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddNOFAbstraction();
```

`AddNOFAbstraction()` registers the scoped daemon service that activates the ambient event publisher. Standard NOF hosts activate daemon services automatically; custom hosts should do the equivalent when entering a scope.

## Auto Injection

`[AutoInject]` lives in the official dependency injection namespace so it can be used with `ServiceLifetime` from a single using:

```csharp
using Microsoft.Extensions.DependencyInjection;

[AutoInject(ServiceLifetime.Scoped)]
public sealed class MyService : IMyService;
```

## JSON And AOT

`NOF.Abstraction` exposes the shared `JsonSerializerOptions.NOF` instance used across the framework.

- It includes `NOFJsonSerializerContext` for common primitive and framework-adjacent types.
- It stays compatible with normal JIT execution.
- In AOT-oriented apps, register your own source-generated contexts before a type is first serialized or deserialized.

```csharp
using System.Text.Json;

JsonSerializerOptions.ConfigureNOFJsonSerializerOptions(options =>
{
    options.TypeInfoResolverChain.Add(MyAppJsonSerializerContext.Default);
});
```

If a type is missing JSON metadata, NOF throws an `InvalidOperationException` that includes the concrete type name and points you at `ConfigureNOFJsonSerializerOptions(...)`.

## License

Apache-2.0
