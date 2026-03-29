# NOF.Test

Testing support package for applications built with the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

`NOF.Test` is designed for **application developers using NOF**, not for testing the NOF framework internals.

It helps you write:

- **Unit tests** for application services and handlers with a lightweight NOF-aware host
- **Integration tests** that exercise DI wiring, invocation context, sender/publisher flows, and in-memory defaults
- **Scenario tests** that need tenant, user, and tracing context without booting a full web server

## What It Provides

### `NOFTestAppBuilder`

Creates a lightweight host builder for tests while still going through the NOF registration pipeline.

```csharp
var builder = NOFTestAppBuilder.Create();
builder.Services.AddScoped<IRequestDispatcher, FakeRequestDispatcher>();

await using var host = await builder.BuildTestHostAsync();
```

### `NOFTestHost`

Wraps the built `IHost` and provides convenient helpers for application tests:

- `CreateScope()`
- `GetRequiredService<T>()`
- `SendAsync(IRequest)`
- `SendAsync<TResponse>(IRequest<TResponse>)`
- `SendAsync(ICommand)`
- `PublishAsync(INotification)`

```csharp
var result = await host.SendAsync(new GetOrderRequest(orderId));
```

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

var result = await scope.SendAsync(new GetOrderRequest(orderId));
```

## Typical Use Cases

### Application-level integration test

```csharp
var builder = NOFTestAppBuilder.Create();

builder.Services.AddScoped<IRequestDispatcher, FakeRequestDispatcher>();

await using var host = await builder.BuildTestHostAsync();

using var scope = host.CreateScope();
scope.SetTenant("tenant-a")
    .SetUser("u-1", "Alice", ["orders.read"]);

var result = await scope.SendAsync(new GetOrderRequest(orderId));
```

### Resolve a scoped service directly

```csharp
await using var host = await NOFTestAppBuilder.Create().BuildTestHostAsync();

using var scope = host.CreateScope();
var repository = scope.GetRequiredService<IOrderRepository>();
```

## Installation

```shell
dotnet add package NOF.Test
```

## Notes

- `NOF.Test` is intended to support **tests of NOF-based applications**
- It is suitable for both **unit tests** and **integration tests**
- It does not require booting ASP.NET Core unless your test specifically needs a web host

## License

Apache-2.0

