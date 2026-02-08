# NOF Framework

[![CI](https://github.com/XmmShp/NOF/actions/workflows/ci.yml/badge.svg)](https://github.com/XmmShp/NOF/actions/workflows/ci.yml)
[![CD](https://github.com/XmmShp/NOF/actions/workflows/cd.yml/badge.svg)](https://github.com/XmmShp/NOF/actions/workflows/cd.yml)

**NOF** (short for **N**eat **O**pinionated **F**ramework) is a modular, convention-driven application framework for .NET that embraces clean architecture principles. It provides a structured approach to building scalable applications with built-in support for CQRS, domain-driven design, transactional outbox, state machines, multi-tenancy, and more.

## Features

- **Clean Architecture** — layered packages (`Domain`, `Contract`, `Application`, `Infrastructure`) enforce separation of concerns
- **CQRS & Messaging** — first-class `IRequest`, `ICommand`, `INotification` abstractions with handler pipelines
- **Source Generators** — compile-time code generation for HTTP endpoint mapping, DI registration, failure definitions, and more
- **Transactional Outbox** — reliable message delivery with inbox/outbox pattern built into EF Core infrastructure
- **State Machines** — declarative, event-driven state machine builder with persistent context
- **Multi-Tenancy** — tenant-aware `DbContext` with automatic model filtering and migration isolation
- **Modular Pipeline** — dependency-aware `IStep` system for ordered service registration and application initialization
- **OpenTelemetry** — built-in tracing, metrics, and logging integration

## Packages

| Package | Description |
|---------|-------------|
| [`NOF.Annotation`](https://www.nuget.org/packages/NOF.Annotation) | Shared attributes (`[AutoInject]`) and enums (`Lifetime`) used across layers |
| [`NOF.Domain`](https://www.nuget.org/packages/NOF.Domain) | Domain layer — entities, aggregate roots, repositories, domain events |
| [`NOF.Contract`](https://www.nuget.org/packages/NOF.Contract) | Contract layer — requests, commands, notifications, `Result<T>`, HTTP endpoint attributes |
| [`NOF.Application`](https://www.nuget.org/packages/NOF.Application) | Application layer — handler abstractions, state machines, caching, unit of work |
| [`NOF.SourceGenerator`](https://www.nuget.org/packages/NOF.SourceGenerator) | Roslyn source generator for `[AutoInject]` DI registration |
| [`NOF.Hosting.AspNetCore`](https://www.nuget.org/packages/NOF.Hosting.AspNetCore) | ASP.NET Core hosting — middleware, OpenAPI, endpoint mapping, JSON configuration |
| [`NOF.Infrastructure.Core`](https://www.nuget.org/packages/NOF.Infrastructure.Core) | Core infrastructure — `INOFAppBuilder`, step pipeline, OpenTelemetry, service wiring |
| [`NOF.Infrastructure.EntityFrameworkCore`](https://www.nuget.org/packages/NOF.Infrastructure.EntityFrameworkCore) | EF Core infrastructure — `NOFDbContext`, repositories, outbox, multi-tenancy |
| [`NOF.Infrastructure.EntityFrameworkCore.PostgreSQL`](https://www.nuget.org/packages/NOF.Infrastructure.EntityFrameworkCore.PostgreSQL) | PostgreSQL provider for NOF EF Core infrastructure |
| [`NOF.Infrastructure.MassTransit`](https://www.nuget.org/packages/NOF.Infrastructure.MassTransit) | MassTransit integration — message bus adapter for commands, events, notifications |
| [`NOF.Infrastructure.MassTransit.RabbitMQ`](https://www.nuget.org/packages/NOF.Infrastructure.MassTransit.RabbitMQ) | RabbitMQ transport for NOF MassTransit infrastructure |
| [`NOF.Infrastructure.StackExchangeRedis`](https://www.nuget.org/packages/NOF.Infrastructure.StackExchangeRedis) | Redis caching infrastructure via StackExchange.Redis |
| [`NOF.Extensions.Auth.Jwt`](https://www.nuget.org/packages/NOF.Extensions.Auth.Jwt) | JWT authentication — token issuance, key derivation, lifecycle management |
| [`NOF.Extensions.Auth.Jwt.Client`](https://www.nuget.org/packages/NOF.Extensions.Auth.Jwt.Client) | JWT client — token validation and client-side token management |

## Quick Start

```csharp
// Program.cs
var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);

builder.AddJwtAuthority();

builder.AddMassTransit()
    .UseRabbitMQ();

builder.AddEFCore<AppDbContext>()
    .AutoMigrate()
    .UsePostgreSQL();

builder.Services.AddRedisCache();

var app = await builder.BuildAsync();

app.MapAllHttpEndpoints();

await app.RunAsync();
```

```csharp
// Define a request
[ExposeToHttpEndpoint(HttpVerb.Get, "/api/orders/{id}")]
public record GetOrderRequest(Guid Id) : IRequest<OrderDto>;

// Implement the handler
public class GetOrderHandler : RequestHandler<GetOrderRequest, OrderDto>
{
    public override async Task<Result<OrderDto>> HandleAsync(
        GetOrderRequest request, CancellationToken cancellationToken)
    {
        // ...
    }
}
```

## Architecture

```
NOF.Annotation          ← Shared attributes (no dependencies)
NOF.Domain              ← Domain entities, aggregate roots, events
NOF.Contract            ← Requests, commands, notifications, DTOs
NOF.Application         ← Handlers, state machines, application services
NOF.Infrastructure.Core ← App builder, pipeline, OpenTelemetry
NOF.Hosting.AspNetCore  ← ASP.NET Core host, endpoints, middleware
NOF.Infrastructure.*    ← Database, messaging, caching providers
NOF.Extensions.*        ← Optional feature extensions
```

## Documentation

Full API documentation is available at the [GitHub Pages site](https://xmmshp.github.io/NOF/).

## License

This project is licensed under the [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0).
