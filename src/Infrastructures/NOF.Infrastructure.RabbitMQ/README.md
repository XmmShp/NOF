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

builder.Services.AddRabbitMQ(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
    options.PrefetchCount = 8;
    options.RequeueOnConsumerFailure = true;
});

builder.Services.AddRabbitMQBackplane(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
});
```

You can configure RabbitMQ either through `ConnectionString` or the individual `HostName`, `Port`, `UserName`, `Password`, and `VirtualHost` properties on `RabbitMQOptions`.

Command exchanges, queues, and routing keys all use the command route name. Both producers and consumers declare this topology idempotently, so commands can wait in the durable queue while no consumer instance is running; multiple consumer instances compete on the same queue.

All command and notification publishes are mandatory. NOF declares two shared failure topologies in the current RabbitMQ virtual host:

- `nof.io-vii.com.unroutable.exchange` routes messages without a matching business queue to `nof.io-vii.com.unroutable.queue`.
- `nof.io-vii.com.dead-letter.exchange` receives rejected, expired, length-limited, and delivery-limited messages from business queues in `nof.io-vii.com.dead-letter.queue`.

Both shared exchanges are topic exchanges with a `#` binding. Messages include `nof-original-exchange` and `nof-original-routing-key` headers for operational replay. A message routed through the alternate exchange counts as a successful mandatory publish; if neither the business topology nor the alternate topology can route it, the publish fails and the NOF outbox retries it.

The alternate-exchange and dead-letter-exchange names are fixed NOF topology invariants rather than application options. Existing exchanges and queues declared without these arguments must be drained and recreated during the upgrade because RabbitMQ does not permit redeclaration with different arguments.

When upgrading from a version that named command queues after handler types, drain and remove the old handler queue bindings before enabling the route-named queue. Keeping both bindings active causes RabbitMQ to route one command to both queues.

Consumer failures caused by transient infrastructure errors are requeued by default. Poison messages that cannot be routed by NOF, such as messages missing type metadata, are rejected without requeueing.

The backplane implementation uses dedicated `nof.backplane.*` fanout exchanges and exclusive auto-delete queues per subscriber. It does not reuse the existing command or notification task distribution topology.

## Dependencies

- [`NOF.Infrastructure`](https://www.nuget.org/packages/NOF.Infrastructure)
- [`RabbitMQ.Client`](https://www.nuget.org/packages/RabbitMQ.Client)
