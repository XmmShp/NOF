# NOF.Contract

Contract layer package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Defines the messaging contracts and shared models that form the public API surface of your application. This package contains request/command/notification interfaces, the `Result<T>` type, and HTTP endpoint attributes with source generation support.

## Key Abstractions

### Messages

```csharp
// Request with response
public record GetOrderRequest(Guid Id) : IRequest<OrderDto>;

// Request without response
public record ArchiveOrderRequest(Guid Id) : IRequest;

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
return Result.Fail(404, "Order not found");
```

### `[ExposeToHttpEndpoint]`

Marks a request type for automatic HTTP endpoint generation via source generator.

```csharp
[ExposeToHttpEndpoint(HttpVerb.Get, "/api/orders/{id}")]
public record GetOrderRequest(Guid Id) : IRequest<OrderDto>;

[ExposeToHttpEndpoint(HttpVerb.Post, "/api/orders")]
public record CreateOrderRequest(string ProductName, int Quantity) : IRequest<Guid>;
```

### Other Annotations

- **`[RequirePermission]`** — declares required permissions for an endpoint
- **`[AllowAnonymous]`** — marks an endpoint as publicly accessible
- **`[EndpointName]`** / **`[EndpointDescription]`** — OpenAPI metadata
- **`[Summary]`** — adds summary documentation to generated endpoints

## Installation

```shell
dotnet add package NOF.Contract
```

## License

Apache-2.0
