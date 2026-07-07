# NOF.Test

Testing harness for applications built with the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

`NOF.Test` is designed for application developers using NOF who want to test Domain/Application/Infrastructure behavior with the same DI, middleware, tenant, user, and local RPC semantics used by the real app.

It helps you write:

- Handler and service tests with a lightweight NOF-aware host
- Local RPC scenario tests without booting a full web server
- Integration-style tests that exercise tenant, user, tracing, caching, persistence, and in-memory event flows

## What It Provides

### `NOFTestAppBuilder`

Creates a lightweight host builder for tests while still going through the NOF registration pipeline.

```csharp
var builder = NOFTestAppBuilder.Create()
    .AddApplicationPartOf<MyRpcServer>()
    .AddRpcServer<MyRpcServer>()
    .AddInMemoryPersistence()
    .AddLocalRpcClient<IMyServiceClient, LocalMyServiceClient>();

await using var host = await builder.BuildTestHostAsync();
```

Common builder helpers:

- `ConfigureServices(...)`
- `AddApplicationPartOf<TMarker>()`
- `AddApplicationPart(Assembly)`
- `AddRpcServer<TRpcServer>()`
- `AddInMemoryPersistence()`
- `AddLocalRpcClient<TService, TImplementation>()`

### `NOFTestHost`

Wraps the built `IHost` and provides convenient helpers for application tests:

- `CreateScope()`
- `CreateScope(configure => ...)`
- `GetRequiredService<T>()`
- `ExecuteAsync(scope => ...)`
- `CallAsync<TClient, TResult>(...)`
- `SendAsync<TCommand>(...)`
- `PublishAsync<TNotification>(...)`

### `NOFTestScope`

Represents a scoped test execution context. It makes it easy to configure execution context before invoking application logic:

- `SetTenant(...)`
- `SetTracing(...)`
- `SetUser(...)`
- `SetAnonymousUser()`
- `SetContext(...)`
- `SetContextItem(...)`
- `SetContextItems(...)`
- `RemoveContextItem(...)`
- `GetRpcClient<TClient>()`
- `CallAsync<TClient, TResult>(...)`
- `SendAsync(...)`
- `PublishAsync(...)`

When a test scope is created, NOF also activates scoped daemon services so ambient conveniences such as `Mapper`, `IdGenerator`, and `EventPublisher` behave the same way they do in normal NOF execution scopes.

```csharp
using var scope = host.CreateScope();

scope.SetTenant("tenant-a")
    .SetUser("user-1", "Alice", ["orders.read"]);

var result = await scope.CallAsync<IOrdersServiceClient, Result<GetOrderResponse>>(
    (client, context, cancellationToken) => client.GetOrderAsync(
        new GetOrderRequest { Id = orderId },
        context,
        cancellationToken));
```

You can also keep the full Arrange-Act-Assert flow inside a single scope:

```csharp
await using var host = await NOFTestAppBuilder.Create()
    .AddApplicationPartOf<NOFSampleService>()
    .AddRpcServer<NOFSampleService>()
    .AddInMemoryPersistence()
    .AddLocalRpcClient<INOFSampleServiceClient, LocalNOFSampleServiceClient>()
    .BuildTestHostAsync();

var roots = await host.ExecuteAsync(async scope =>
{
    scope.SetTenant("tenant-a")
        .SetUser("user-1", "Alice")
        .SetContextItem("case", "create-root");

    await scope.CallAsync<INOFSampleServiceClient, Result>(
        (client, context, cancellationToken) => client.CreateConfigNodeAsync(
            new CreateConfigNodeRequest { Name = "root-a" },
            context,
            cancellationToken));

    return await scope.CallAsync<INOFSampleServiceClient, Result<GetRootConfigNodesResponse>>(
        (client, context, cancellationToken) => client.GetRootConfigNodesAsync(
            new GetRootConfigNodesRequest(),
            context,
            cancellationToken));
});
```

See [`sample-tests/NOF.Sample.Tests`](file:///Users/bytedance/Workspace/koala-dir/NOF/sample-tests/NOF.Sample.Tests) for a runnable sample harness project.

## Installation

```shell
dotnet add package NOF.Test
```

## License

Apache-2.0
