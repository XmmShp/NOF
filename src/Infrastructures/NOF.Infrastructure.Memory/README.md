# NOF.Infrastructure.Memory

In-memory infrastructure package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides `NOF.Infrastructure.Memory` in-memory implementations used as development/testing fallbacks:

- `InMemoryCacheService`
- In-memory riders (`InMemoryCommandRider`, `InMemoryNotificationRider`, `InMemoryRequestRider`)
- `InMemoryEventPublisher`
- In-memory persistence store/session/repositories/unit-of-work/transaction manager
- `MemoryPersistenceWarningHostedService`

These implementations are process-local and non-durable, and should not be used as production persistence.

## Dependencies

- [`NOF.Infrastructure.Abstraction`](https://www.nuget.org/packages/NOF.Infrastructure.Abstraction)

## Installation

```shell
dotnet add package NOF.Infrastructure.Memory
```

## License

Apache-2.0
