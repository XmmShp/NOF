---
description: How to set up MassTransit with RabbitMQ for distributed messaging in a NOF application
---

# Add MassTransit Messaging with RabbitMQ

NOF integrates MassTransit for distributed commands, requests, and notifications across services.

## 1. Add NuGet Packages

```bash
dotnet add package NOF.Infrastructure.MassTransit.RabbitMQ
```

## 2. Register in Program.cs

```csharp
builder.AddMassTransit()
    .UseRabbitMQ();
```

## 3. Configure RabbitMQ

In `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "rabbitmq": "amqp://guest:guest@localhost:5672"
  }
}
```

## 4. Distributed Commands

Commands are fire-and-forget messages sent to a specific consumer.

```csharp
// In Contract project — shared between services
public record ProcessPaymentCommand(long OrderId, decimal Amount) : ICommand;

// In Application project — consumer service
public class ProcessPaymentHandler : ICommandHandler<ProcessPaymentCommand>
{
    public async Task HandleAsync(ProcessPaymentCommand command, CancellationToken ct)
    {
        // Process payment
    }
}

// Sending from another service
public class PlaceOrderHandler : IRequestHandler<PlaceOrderRequest>
{
    private readonly ICommandSender _commandSender;

    public PlaceOrderHandler(ICommandSender commandSender)
    {
        _commandSender = commandSender;
    }

    public async Task<Result> HandleAsync(PlaceOrderRequest request, CancellationToken ct)
    {
        // Send to a specific service endpoint
        await _commandSender.SendAsync(
            new ProcessPaymentCommand(request.OrderId, request.Amount),
            destinationEndpointName: "payment-service",
            cancellationToken: ct);

        return Result.Success();
    }
}
```

## 5. Distributed Requests (RPC)

Requests cross service boundaries and return a response.

```csharp
// Shared contract
[ExposeToHttpEndpoint(HttpVerb.Get, "api/inventory/{productId}")]
public record CheckInventoryRequest(long ProductId) : IRequest<CheckInventoryResponse>;
public record CheckInventoryResponse(int AvailableQuantity);

// Sending to a remote service
var result = await _requestSender.SendAsync(
    new CheckInventoryRequest(productId),
    destinationEndpointName: "inventory-service",
    cancellationToken: ct);
```

## 6. Distributed Notifications (Pub/Sub)

Notifications are broadcast to all subscribers across services.

```csharp
// Shared contract
public record OrderCompletedNotification(long OrderId) : INotification;

// Publisher
await _notificationPublisher.PublishAsync(
    new OrderCompletedNotification(orderId), ct);

// Subscriber (in any service that references the contract)
public class LogOrderCompletedHandler : INotificationHandler<OrderCompletedNotification>
{
    public async Task HandleAsync(OrderCompletedNotification notification, CancellationToken ct)
    {
        // React to the event
    }
}
```

## 7. Headers Propagation

NOF automatically propagates headers (identity, tenant, tracing) across service boundaries. You can also add custom headers:

```csharp
var headers = new Dictionary<string, string?>
{
    ["X-Correlation-Id"] = correlationId
};

await _commandSender.SendAsync(command, headers, cancellationToken: ct);
```

## Notes

- MassTransit is configured with OpenTelemetry tracing and metrics automatically.
- Commands, requests, and notifications all flow through the NOF middleware pipeline (identity, tenant, tracing, authorization).
- The transactional outbox ensures messages are only published after the database transaction commits.
- Use `ICommandSender`, `IRequestSender`, and `INotificationPublisher` — never use MassTransit APIs directly.
- Handler discovery is automatic via source generators.
