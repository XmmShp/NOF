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

## Usage

`NOF.Infrastructure` no longer includes memory implementations by default.
To use in-memory infrastructure, add this package and register it explicitly:

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);

builder.AddMemoryInfrastructure();
```

## Dependencies

- [`NOF.Hosting.Abstraction`](https://www.nuget.org/packages/NOF.Hosting.Abstraction)

## Installation

```shell
dotnet add package NOF.Infrastructure.Memory
```

## License

Apache-2.0

