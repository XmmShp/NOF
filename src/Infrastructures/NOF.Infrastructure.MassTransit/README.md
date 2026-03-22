# NOF.Infrastructure.MassTransit

MassTransit messaging infrastructure package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the message bus integration layer using MassTransit. Adapts NOF's `ICommandHandler`, `INotificationHandler`, and `IRequestHandler` abstractions to MassTransit consumers, enabling distributed messaging with transactional outbox support.

## Features

- **Handler Adapters** â€?automatically wraps NOF handlers as MassTransit consumers
- **Command Sending** â€?`ICommandSender` implementation via MassTransit send endpoints
- **Event Publishing** â€?`IEventPublisher` for domain event distribution
- **Notification Riding** â€?pub/sub notification delivery
- **Endpoint Name Formatting** â€?convention-based endpoint naming with caching
- **Deferred Sending** â€?`IDeferredCommandSender` for transactional outbox integration

## Usage

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);

builder.AddMassTransit();
```

Handlers are automatically discovered and registered as MassTransit consumers. No manual consumer registration required.

## Installation

```shell
dotnet add package NOF.Infrastructure.MassTransit
```

## License

Apache-2.0

