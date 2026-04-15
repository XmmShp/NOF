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

## License

Apache-2.0
