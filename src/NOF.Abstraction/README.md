# NOF.Abstraction

Cross-cutting abstractions package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides shared contracts and annotations intended for use across layers:

- `[AutoInject]`
- `InMemoryEventHandler<TEvent>` / `IEventPublisher`

## In-Memory Events

NOF provides a scoped in-memory event mechanism for invoking distributed handlers within the same dependency injection scope.

- Any non-null `object` can be used as an in-memory event payload
- `InMemoryEventHandler<TEvent>` handles that payload
- `IEventPublisher` dispatches the event to all handlers resolved from the current scope

This mechanism can be used by domain aggregates, application services, or any other in-scope collaboration that should remain in-process.

## Installation

```shell
dotnet add package NOF.Abstraction
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
