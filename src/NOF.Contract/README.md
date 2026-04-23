# NOF.Contract

Contract layer package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Defines the messaging contracts and shared models that form the public API surface of your application. This package contains `Result<T>`, `Empty`, HTTP endpoint annotations, and other shared attributes used by source generators and hosts.

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

### RPC Contracts

RPC service methods use a strict single-request signature, do not accept `CancellationToken` on the contract surface,
do not end with `Async`, and must return a non-Task, non-`void` value. The return type does not need to be `Result`-based.

```csharp
public record GetOrderRequest(Guid Id);
public record CreateOrderRequest(string ProductName, int Quantity);

public partial interface IOrderService : IRpcService
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
}
```

### Other Annotations

- **`[HttpEndpoint]`** - declares HTTP verb and route metadata for RPC methods
- Route parameters such as `"{id}"` are not supported for RPC HTTP endpoints; put input data on the request object instead
- **`[RequirePermission]`** - declares required permissions for an endpoint
- **`[Summary]`** - adds summary documentation to generated endpoints
- These NOF-specific attributes are all metadata-backed and converge on `MetadataAttribute`

## Installation

```shell
dotnet add package NOF.Contract
```

## License

Apache-2.0
