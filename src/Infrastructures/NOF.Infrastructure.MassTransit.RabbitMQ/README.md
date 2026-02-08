# NOF.Infrastructure.MassTransit.RabbitMQ

RabbitMQ transport package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Extends `NOF.Infrastructure.MassTransit` with RabbitMQ as the message transport. Provides the RabbitMQ-specific bus configuration for MassTransit within NOF applications.

## Usage

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);

builder.AddMassTransit()
    .UseRabbitMQ();
```

RabbitMQ connection settings are resolved from your application configuration (default connection name: `"rabbitmq"`).

## Dependencies

- [`NOF.Infrastructure.MassTransit`](https://www.nuget.org/packages/NOF.Infrastructure.MassTransit)
- [`MassTransit.RabbitMQ`](https://www.nuget.org/packages/MassTransit.RabbitMQ)

## Installation

```shell
dotnet add package NOF.Infrastructure.MassTransit.RabbitMQ
```

## License

Apache-2.0
