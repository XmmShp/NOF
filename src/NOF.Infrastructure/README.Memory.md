# Memory Infrastructure

In-memory infrastructure package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the in-memory implementations now built into `NOF.Infrastructure`, used as the default development/testing fallback:

- `MemoryCacheService`
- In-memory riders (`MemoryCommandRider`, `MemoryNotificationRider`)
- `EventPublisher` (in-process event dispatch)
- Optional zero-configuration SQLite in-memory persistence setup for EF Core

These implementations are process-local and non-durable, and should not be used as production infrastructure.

## Usage

`NOF.Infrastructure` no longer includes memory implementations by default.
To use in-memory infrastructure, add this package and register it explicitly:

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaults: true);

builder.AddMemoryInfrastructure();
```

`AddMemoryInfrastructure()` now auto-registers `NOFDbContext` for zero-configuration setup.

If your app uses EF Core and you want an all-in-one in-memory setup (including SQLite provider, tenancy mode and auto-migration), use:

```csharp
builder.AddMemoryInfrastructure<AppDbContext>();
```

## Dependencies

- [`NOF.Hosting.Abstraction`](https://www.nuget.org/packages/NOF.Hosting.Abstraction)

## Installation

```shell
dotnet add package NOF.Infrastructure
```

## License

Apache-2.0
