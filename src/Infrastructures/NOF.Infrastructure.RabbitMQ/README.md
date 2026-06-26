# NOF.Infrastructure.RabbitMQ

RabbitMQ integration for the NOF Framework - transport adapters for NOF command/notification dispatch and a dedicated `IBackplane` implementation using the official RabbitMQ client.

## Installation

```bash
dotnet add package NOF.Infrastructure.RabbitMQ
```

## Usage

```csharp
using NOF.Hosting.AspNetCore;
using NOF.Infrastructure.RabbitMQ;

var builder = NOFWebApplicationBuilder.Create(args);

builder.AddRabbitMQ(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
    options.PrefetchCount = 8;
    options.RequeueOnConsumerFailure = true;
});

builder.AddRabbitMQBackplane(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
});
```

You can configure RabbitMQ either through `ConnectionString` or the individual `HostName`, `Port`, `UserName`, `Password`, and `VirtualHost` properties on `RabbitMQOptions`.

Consumer failures caused by transient infrastructure errors are requeued by default. Poison messages that cannot be routed by NOF, such as messages missing type metadata, are rejected without requeueing.

The backplane implementation uses dedicated `nof.backplane.*` fanout exchanges and exclusive auto-delete queues per subscriber. It does not reuse the existing command or notification task distribution topology.

## Dependencies

- [`NOF.Infrastructure`](https://www.nuget.org/packages/NOF.Infrastructure)
- [`RabbitMQ.Client`](https://www.nuget.org/packages/RabbitMQ.Client)
