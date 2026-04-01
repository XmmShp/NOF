# NOF.Infrastructure.RabbitMQ

RabbitMQ integration for the NOF Framework — message bus adapter for commands, events, and notifications using the official RabbitMQ client.

## Installation

```bash
dotnet add package NOF.Infrastructure.RabbitMQ
```

## Usage

```csharp
using NOF.Hosting;
using NOF.Infrastructure.RabbitMQ;

var builder = NOFApp.CreateBuilder();
builder.AddRabbitMQ(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.UserName = "guest";
    options.Password = "guest";
    options.VirtualHost = "/";
    options.ExchangeName = "nof.exchange";
});
```

## Dependencies

- [`NOF.Infrastructure`](https://www.nuget.org/packages/NOF.Infrastructure)
- [`RabbitMQ.Client`](https://www.nuget.org/packages/RabbitMQ.Client)
