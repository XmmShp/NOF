# NOF.Contract

Contract layer package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Defines the messaging contracts and shared models that form the public API surface of your application. This package contains `Result<T>`, `StreamingResult<T>`, `Empty`, HTTP endpoint annotations, and other shared attributes used by source generators and hosts.

`Context` remains a generic execution-context carrier for explicit metadata passing across boundaries.

## Key Abstractions

### Messages

```csharp
// Request with response
public record GetOrderRequest(Guid Id);

// Request without payload response
public record ArchiveOrderRequest(Guid Id);

// Fire-and-forget command
public record SendEmailCommand(string To, string Subject, string Body);

// Publish/subscribe notification
public record OrderCreatedNotification(Guid OrderId);
```

### Result Type

```csharp
// Success
return Result.Success(orderDto);

// Failure
return Result.Fail("404", "Order not found");
```

### Streaming Result Type

Use `StreamingResult<T>` when an RPC method returns a server-side stream. This keeps the contract surface synchronous while still allowing generated clients to return `Task<StreamingResult<T>>`.

```csharp
public record WatchOrdersRequest(Guid CustomerId);
public record OrderEvent(Guid OrderId, string Status);

public interface IOrderService : IRpcService
{
    [HttpEndpoint(HttpVerb.Get, "/api/orders/watch")]
    StreamingResult<OrderEvent> Watch(WatchOrdersRequest request);
}
```

### RPC Contracts

RPC service methods use a strict single-request signature, do not accept `CancellationToken` on the contract surface,
do not end with `Async`, and must return a non-Task, non-`void` value. Unary methods may return plain payload types or `Result`-based types. Streaming methods must return `StreamingResult<T>`.

```csharp
public record GetOrderRequest(Guid Id);
public record CreateOrderRequest(string ProductName, int Quantity);

[TransportOverHttp(HttpRpcStyle.ControllerRpc)]
public interface IOrderService : IRpcService
{
    [Summary("Get order")]
    [HttpEndpoint(HttpVerb.Get, "/api/orders/get")]
    Result<OrderDto> Get(GetOrderRequest request);

    [Summary("Create order")]
    [HttpEndpoint(HttpVerb.Post, "/api/orders")]
    Result<OrderDto> Create(CreateOrderRequest request);

    [Summary("Archive order")]
    [HttpEndpoint(HttpVerb.Post, "/api/orders/archive")]
    Empty Archive(ArchiveOrderRequest request);

    [Summary("Watch order events")]
    [HttpEndpoint(HttpVerb.Get, "/api/orders/watch")]
    StreamingResult<OrderEvent> Watch(WatchOrdersRequest request);
}
```

Declaring `TransportOverHttp` also generates the transport client next to the contract. For the example above, NOF generates the protocol-neutral `IOrderServiceClient : IRpcClient<IOrderService>` interface and a public partial `HttpOrderServiceClient` implementation. The generic `IRpcClient<TRpcService>` base is the explicit relationship between service and client contracts; transport and local-client generators do not need to infer that relationship from type names.

```csharp
builder.Services.AddHttpClient<IOrderServiceClient, HttpOrderServiceClient>(client =>
    client.BaseAddress = new Uri("https://orders.example/"));
```

Generated HTTP request URIs are relative to `HttpClient.BaseAddress`. This preserves path prefixes such as `https://webui.example/bff/orders/` when the client is routed through a BFF or reverse proxy. When `BaseAddress` contains a path prefix, end it with `/` so standard URI resolution treats the complete path as a directory.

The generated constructor accepts `IRequestOutboundPipelineExecutor` as an optional dependency. When a hosting package registers it, outbound middleware participates in the call; when it is absent, the client sends the HTTP request directly.

For RPC contracts that must remain inside one process, use `TransportOverMemory` and omit `HttpEndpoint` metadata:

```csharp
[TransportOverMemory]
public interface IBackOfficeService : IRpcService
{
    Result<DashboardDto> GetDashboard(GetDashboardRequest request);
}
```

NOF still generates `IBackOfficeServiceClient`, and `NOF.Infrastructure` generates the corresponding local client from its `RpcServer<IBackOfficeService>` implementation. Register the server and local client in a Blazor Server or other in-process host:

```csharp
builder.AddRpcServer<BackOfficeService>();
builder.Services.ReplaceOrAddScoped<IBackOfficeServiceClient, LocalBackOfficeServiceClient>();
```

No HTTP client is generated for a memory-only contract. Each server transport decides whether a registration applies to it from the contract's `TransportOverAttribute`; the built-in HTTP transport ignores `TransportOverMemory`, so no HTTP endpoint is mapped.

### Other Annotations

- **`[HttpEndpoint]`** - declares HTTP verb and route metadata for RPC methods
- Every RPC service contract must declare exactly one `TransportOverAttribute` implementation
- **`[TransportOverHttp]`** - declares the HTTP RPC style and an optional contract-level route prefix, and generates the matching `Http...Client`
- **`[TransportOverMemory]`** - declares that the RPC service is intended for its generated local client; the built-in HTTP transport does not expose it, and methods must not declare `[HttpEndpoint]`
- HTTP route prefixes are application-relative paths. A leading `/` is optional and one trailing `/` is normalized away; empty or whitespace values, absolute URIs, query strings, fragments, route parameters, escaped characters, backslashes, empty path segments, and `.` or `..` segments are rejected at compile time
- JSON-RPC contracts use the service route prefix and operation names; when the prefix is omitted, the endpoint is `/`
- JSON-RPC methods must not declare `[HttpEndpoint]`
- JSON-RPC streaming methods use SSE; each event carries a JSON-RPC response envelope for one stream item
- Route parameters such as `"{id}"` are not supported for RPC HTTP endpoints; put input data on the request object instead
- Streaming HTTP endpoints use server-sent events when hosted by `NOF.Hosting.AspNetCore`
- **`[RequirePermission]`** - declares required permissions for an endpoint
- **`[Summary]`** - adds summary documentation to generated endpoints
- These NOF-specific attributes are all metadata-backed and converge on `MetadataAttribute`

## Context

Use `Context` for explicit per-call metadata. Header snapshots may be copied into `Context.Items` by transport/infrastructure components. String item keys use ordinal, case-insensitive comparison to preserve HTTP header semantics; non-string keys retain their default equality behavior.

Runtime outbound authentication directives are provided by `NOF.Application`, not `NOF.Contract`.

## Installation

```shell
dotnet add package NOF.Contract
```

## License

Apache-2.0
