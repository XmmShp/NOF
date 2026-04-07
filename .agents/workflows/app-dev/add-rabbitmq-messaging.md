---
description: Add RabbitMQ-based distributed messaging to a NOF application
---

# Add RabbitMQ Messaging

## 1. Add Package

```bash
dotnet add package NOF.Infrastructure.RabbitMQ
```

## 2. Register in Program.cs

```csharp
builder.AddRabbitMQ();
// or:
// builder.AddRabbitMQ("rabbitmq");
```

## 3. Configure Connection String

```json
{
  "ConnectionStrings": {
    "rabbitmq": "Host=localhost;Port=5672;UserName=guest;Password=guest;VirtualHost=/"
  }
}
```

## 4. Send Messages

```csharp
await _commandSender.SendAsync(command, destinationEndpointName: "order-service", cancellationToken: ct);
await _notificationPublisher.PublishAsync(notification, ct);
```

## 5. Notes

- Prefer NOF abstractions (generated RPC clients, `ICommandSender`, `INotificationPublisher`) instead of direct RabbitMQ client usage.
- Tenant, user, and tracing headers flow through the NOF pipeline automatically.
- For transactional reliability with persistence, use deferred send/publish and outbox support.
