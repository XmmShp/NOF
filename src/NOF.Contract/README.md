# NOF.Contract

Contract layer package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Defines the messaging contracts and shared models that form the public API surface of your application. This package contains request/command/notification interfaces, the `Result<T>` type, and metadata-backed HTTP endpoint annotations with source generation support.

## Key Abstractions

### Messages

```csharp
// Request with response
public record GetOrderRequest(Guid Id);

// Request without response
public record ArchiveOrderRequest(Guid Id);

// Fire-and-forget command
public record SendEmailCommand(string To, string Subject, string Body) : ICommand;

// Publish/subscribe notification
public record OrderCreatedNotification(Guid OrderId) : INotification;
```

### Result Type

```csharp
// Success
return Result.Success(orderDto);

// Failure
return Result.Fail("404", "Order not found");
```

### RPC Contracts

RPC service methods use a strict single-request signature and do not accept `CancellationToken` on the contract surface.

```csharp
public record GetOrderRequest(Guid Id);
public record CreateOrderRequest(string ProductName, int Quantity);

public partial interface IOrderService : IRpcService
{
    [Summary("Get order")]
    [HttpEndpoint(HttpVerb.Get, "/api/orders/{id}")]
    Task<Result<OrderDto>> GetAsync(GetOrderRequest request);

    [Summary("Create order")]
    [HttpEndpoint(HttpVerb.Post, "/api/orders")]
    Task<Result<OrderDto>> CreateAsync(CreateOrderRequest request);
}
```

### Other Annotations

- **`[HttpEndpoint]`** - declares HTTP verb and route metadata for RPC methods
- **`[RequirePermission]`** - declares required permissions for an endpoint
- **`[Summary]`** - adds summary documentation to generated endpoints
- These NOF-specific attributes are all metadata-backed and converge on `MetadataAttribute`

## Installation

```shell
dotnet add package NOF.Contract
```

## License

Apache-2.0
