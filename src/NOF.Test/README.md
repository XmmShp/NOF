# NOF.Test

Testing support package for applications built with the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

`NOF.Test` is designed for application developers using NOF.

It helps you write:

- Unit tests for application services and handlers with a lightweight NOF-aware host
- Integration tests that exercise DI wiring, invocation context, command sender and notification publisher flows
- Scenario tests that need tenant, user, and tracing context without booting a full web server

## What It Provides

### `NOFTestAppBuilder`

Creates a lightweight host builder for tests while still going through the NOF registration pipeline.

```csharp
var builder = NOFTestAppBuilder.Create();
await using var host = await builder.BuildTestHostAsync();
```

### `NOFTestHost`

Wraps the built `IHost` and provides convenient helpers for application tests:

- `CreateScope()`
- `GetRequiredService<T>()`
- `SendAsync(ICommand)`
- `PublishAsync(INotification)`

### `NOFTestScope`

Represents a scoped test execution context. It makes it easy to configure ambient context before invoking application logic:

- `SetTenant(...)`
- `SetTracing(...)`
- `SetUser(...)`
- `SetAnonymousUser()`

```csharp
using var scope = host.CreateScope();

scope.SetTenant("tenant-a")
    .SetUser("user-1", "Alice", ["orders.read"]);

await scope.SendAsync(new RebuildProjectionCommand("orders"));
await scope.PublishAsync(new OrderSyncedNotification(orderId));
```

## Installation

```shell
dotnet add package NOF.Test
```

## License

Apache-2.0
