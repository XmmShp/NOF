---
description: Replace NOF's in-memory command and notification riders with RabbitMQ
---

# Add RabbitMQ Messaging

## 1. Add the Package to the Host

```bash
dotnet add package NOF.Infrastructure.RabbitMQ
```

## 2. Register RabbitMQ

```csharp
builder.Services.AddRabbitMQ(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
});
```

`AddRabbitMQ(...)` configures options, replaces `ICommandRider` and `INotificationRider`, and registers the RabbitMQ consumer hosted service.

## 3. Configure the Connection

`RabbitMQOptions.ConnectionString` accepts either an AMQP URI or key/value form:

```json
{
  "ConnectionStrings": {
    "rabbitmq": "Host=localhost;Port=5672;UserName=guest;Password=guest;VirtualHost=/"
  }
}
```

The options also expose durability, auto-delete, prefetch, requeue, and publisher-confirmation settings.

## 4. Send with the NOF Abstractions

```csharp
await commandSender.SendAsync(command, context, cancellationToken);
await notificationPublisher.PublishAsync(notification, context, cancellationToken);
```

Transport routing comes from source-generated command and notification handler metadata; callers do not specify a queue or exchange.

For writes that must commit with outgoing work:

```csharp
await commandSender.DeferSendAsync(command, context, cancellationToken);
await notificationPublisher.DeferPublishAsync(notification, context, cancellationToken);
await dbContext.SaveChangesAsync(cancellationToken);
```

Use the ordered deferred variants only when messages form one strict stream. The framework qualifies the order key with the producer service name and allocates sequence numbers transactionally.

## 5. Optional RabbitMQ Backplane

`builder.Services.AddRabbitMQBackplane(...)` replaces `IBackplane` only. It is independent from `AddRabbitMQ(...)`; enable it when application backplane traffic should also use RabbitMQ.
