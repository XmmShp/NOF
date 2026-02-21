---
description: How to add a CQRS request handler with HTTP endpoint in a NOF application
---

# Add a Request Handler

NOF uses CQRS with `IRequest`, `ICommand`, and `INotification`. Request handlers return `Result<T>` and can be auto-mapped to HTTP endpoints.

## Request with Typed Response (Query)

### 1. Define the request and response in the Contract project

```csharp
using NOF.Contract;
using System.ComponentModel;

[AllowAnonymous]  // Skip authentication (remove for protected endpoints)
[ExposeToHttpEndpoint(HttpVerb.Get, "api/orders/{id}")]
[Summary("Get order by ID")]
[EndpointDescription("Retrieves a single order by its unique identifier")]
[Category("Orders")]
public record GetOrderRequest(long Id) : IRequest<GetOrderResponse>;

public record GetOrderResponse(long Id, string CustomerName, string Status);
```

### 2. Implement the handler in the Application project

```csharp
using NOF.Application;
using NOF.Contract;

public class GetOrderHandler : IRequestHandler<GetOrderRequest, GetOrderResponse>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<GetOrderResponse>> HandleAsync(
        GetOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FindAsync(
            OrderId.Of(request.Id), cancellationToken);

        if (order is null)
        {
            return Result.Fail(404, "Order not found");
        }

        return new GetOrderResponse(
            (long)order.Id,
            order.CustomerName,
            order.Status.ToString());
    }
}
```

## Request without Response (Command-style via Request)

```csharp
// Contract
[ExposeToHttpEndpoint(HttpVerb.Post, "api/orders")]
[Summary("Create a new order")]
[Category("Orders")]
public record CreateOrderRequest(string CustomerName) : IRequest;

// Handler
public class CreateOrderHandler : IRequestHandler<CreateOrderRequest>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _uow;

    public CreateOrderHandler(IOrderRepository orderRepository, IUnitOfWork uow)
    {
        _orderRepository = orderRepository;
        _uow = uow;
    }

    public async Task<Result> HandleAsync(
        CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = Order.Create(request.CustomerName);
        _orderRepository.Add(order);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

## Fire-and-Forget Command

Commands are dispatched without waiting for a response. Useful for background processing.

```csharp
// Contract
public record ProcessPaymentCommand(long OrderId, decimal Amount) : ICommand;

// Handler
public class ProcessPaymentHandler : ICommandHandler<ProcessPaymentCommand>
{
    public async Task HandleAsync(
        ProcessPaymentCommand command, CancellationToken cancellationToken)
    {
        // Process payment asynchronously
    }
}

// Sending a command from a request handler:
public class PlaceOrderHandler : IRequestHandler<PlaceOrderRequest>
{
    private readonly ICommandSender _commandSender;

    public PlaceOrderHandler(ICommandSender commandSender)
    {
        _commandSender = commandSender;
    }

    public async Task<Result> HandleAsync(
        PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        await _commandSender.SendAsync(
            new ProcessPaymentCommand(request.OrderId, request.Amount),
            cancellationToken);
        return Result.Success();
    }
}
```

## Publish/Subscribe Notification

Notifications are delivered to all registered handlers.

```csharp
// Contract
public record OrderShippedNotification(long OrderId) : INotification;

// Handler (can have multiple handlers for the same notification)
public class SendShippingEmailHandler : INotificationHandler<OrderShippedNotification>
{
    public async Task HandleAsync(
        OrderShippedNotification notification, CancellationToken cancellationToken)
    {
        // Send email
    }
}

// Publishing from a handler:
private readonly INotificationPublisher _publisher;

await _publisher.PublishAsync(
    new OrderShippedNotification(orderId), cancellationToken);
```

## Endpoint Metadata Attributes

| Attribute | Purpose |
|-----------|---------|
| `[ExposeToHttpEndpoint(HttpVerb, route)]` | Map to HTTP endpoint |
| `[AllowAnonymous]` | Skip authentication |
| `[Summary("...")]` | OpenAPI summary |
| `[EndpointDescription("...")]` | OpenAPI description |
| `[Category("...")]` | OpenAPI tag/group |

## Notes

- Handlers are auto-discovered — no manual DI registration needed (via source-generated `AddAllHandlers()`).
- Route parameters like `{id}` are matched to request record properties by name.
- `Result.Fail(statusCode, message)` maps to HTTP status codes automatically.
- `Result.Success()` returns HTTP 200; `Result<T>` serializes the value as JSON.
- Use `IRequestSender` to send requests programmatically (e.g., service-to-service).
