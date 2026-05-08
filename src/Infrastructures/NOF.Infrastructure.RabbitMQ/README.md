# NOF.Infrastructure.RabbitMQ

RabbitMQ integration for the NOF Framework - a transport adapter for NOF command and notification dispatch using the official RabbitMQ client.

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
});
```

You can configure RabbitMQ either through `ConnectionString` or the individual `HostName`, `Port`, `UserName`, `Password`, and `VirtualHost` properties on `RabbitMQOptions`.

## Dependencies

- [`NOF.Infrastructure`](https://www.nuget.org/packages/NOF.Infrastructure)
- [`RabbitMQ.Client`](https://www.nuget.org/packages/RabbitMQ.Client)
