# NOF.Contract

Contract layer package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Defines the messaging contracts and shared models that form the public API surface of your application. This package contains `Result<T>`, `StreamingResult<T>`, `Empty`, HTTP endpoint annotations, and other shared attributes used by source generators and hosts.

`Context` remains a generic execution-context carrier for explicit metadata passing across boundaries. Runtime authentication directives such as service-token or token-exchange markers are intentionally defined outside this package.

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

### Other Annotations

- **`[HttpEndpoint]`** - declares HTTP verb and route metadata for RPC methods
- Route parameters such as `"{id}"` are not supported for RPC HTTP endpoints; put input data on the request object instead
- Streaming HTTP endpoints use server-sent events when hosted by `NOF.Hosting.AspNetCore`
- **`[RequirePermission]`** - declares required permissions for an endpoint
- **`[Summary]`** - adds summary documentation to generated endpoints
- These NOF-specific attributes are all metadata-backed and converge on `MetadataAttribute`

## Context

Use `Context` for explicit per-call metadata. Header snapshots may be copied into `Context.Items` by transport/infrastructure components.

Runtime outbound authentication directives are provided by `NOF.Application`, not `NOF.Contract`.

## Installation

```shell
dotnet add package NOF.Contract
```

## License

Apache-2.0
