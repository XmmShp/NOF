# NOF Framework

[![CI](https://github.com/XmmShp/NOF/actions/workflows/ci.yml/badge.svg)](https://github.com/XmmShp/NOF/actions/workflows/ci.yml)
[![CD](https://github.com/XmmShp/NOF/actions/workflows/cd.yml/badge.svg)](https://github.com/XmmShp/NOF/actions/workflows/cd.yml)

**NOF** (short for **N**eat **O**pinionated **F**ramework) is a modular, convention-driven application framework for .NET that embraces clean architecture principles. It provides a structured approach to building scalable applications with built-in support for CQRS, domain-driven design, transactional outbox, state machines, multi-tenancy, and observability.

## Features

- **Clean Architecture** - layered packages (`Domain`, `Contract`, `Application`, `Infrastructure`) enforce separation of concerns
- **CQRS & Messaging** - first-class `IRequest`, `ICommand`, `INotification` abstractions with handler pipelines
- **Source Generators** - compile-time code generation for explicit HTTP service endpoint mapping, DI registration, and failure definitions
- **Transactional Outbox** - reliable message delivery with inbox/outbox pattern built into EF Core infrastructure
- **State Machines** - declarative, event-driven state machine builder with persistent context
- **Multi-Tenancy** - tenant-aware persistence and infrastructure integration
- **Modular Pipeline** - dependency-aware `IStep` system for ordered service registration and application initialization
- **OpenTelemetry** - built-in tracing, metrics, and logging integration

## Packages

| Package | Description |
|---------|-------------|
| [`NOF.Domain`](https://www.nuget.org/packages/NOF.Domain) | Domain layer - entities, aggregate roots, repositories, domain events |
| [`NOF.Contract`](https://www.nuget.org/packages/NOF.Contract) | Contract layer - requests, commands, notifications, `Result<T>`, HTTP endpoint attributes |
| [`NOF.Application`](https://www.nuget.org/packages/NOF.Application) | Application layer - handler abstractions, state machines, caching, unit of work |
| [`NOF.Hosting.Abstraction`](https://www.nuget.org/packages/NOF.Hosting.Abstraction) | Hosting abstractions - builder contracts, step contracts, dependency ordering |
| [`NOF.Infrastructure`](https://www.nuget.org/packages/NOF.Infrastructure) | Core infrastructure - app builder implementation, pipeline, OpenTelemetry, service wiring |
| [`NOF.UI`](https://www.nuget.org/packages/NOF.UI) | Reusable UI primitives - authorization components, browser storage, client cache services |
| [`NOF.Application.Extension.Redis`](https://www.nuget.org/packages/NOF.Application.Extension.Redis) | Application Redis extension - advanced Redis cache abstractions built on top of `ICacheService` |
| [`NOF.Hosting.AspNetCore`](https://www.nuget.org/packages/NOF.Hosting.AspNetCore) | ASP.NET Core hosting - middleware, OpenAPI, service endpoint mapping, JSON configuration |
| [`NOF.Hosting.BlazorWebAssembly`](https://www.nuget.org/packages/NOF.Hosting.BlazorWebAssembly) | Blazor WebAssembly hosting - WebAssembly host builder integration |
| [`NOF.Hosting.Maui`](https://www.nuget.org/packages/NOF.Hosting.Maui) | .NET MAUI hosting - MAUI app builder integration for cross-platform applications |
| [`NOF.Contract.Extension.Authorization.Jwt`](https://www.nuget.org/packages/NOF.Contract.Extension.Authorization.Jwt) | JWT authorization contract - JWT service contracts, token models, and JWKS definitions |
| [`NOF.Infrastructure.Extension.Authorization.Jwt`](https://www.nuget.org/packages/NOF.Infrastructure.Extension.Authorization.Jwt) | JWT authorization and authority - token issuance, key rotation, JWKS |
| [`NOF.Infrastructure.EntityFrameworkCore`](https://www.nuget.org/packages/NOF.Infrastructure.EntityFrameworkCore) | EF Core infrastructure - `NOFDbContext`, repositories, outbox, multi-tenancy |
| [`NOF.Infrastructure.EntityFrameworkCore.PostgreSQL`](https://www.nuget.org/packages/NOF.Infrastructure.EntityFrameworkCore.PostgreSQL) | PostgreSQL provider for NOF EF Core infrastructure |
| [`NOF.Infrastructure.MassTransit`](https://www.nuget.org/packages/NOF.Infrastructure.MassTransit) | MassTransit integration - message bus adapter for commands, events, notifications |
| [`NOF.Infrastructure.MassTransit.RabbitMQ`](https://www.nuget.org/packages/NOF.Infrastructure.MassTransit.RabbitMQ) | RabbitMQ transport for NOF MassTransit infrastructure |
| [`NOF.Infrastructure.StackExchangeRedis`](https://www.nuget.org/packages/NOF.Infrastructure.StackExchangeRedis) | Redis caching infrastructure via StackExchange.Redis |

## Quick Start

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaults: true);

builder.AddJwtAuthority();

builder.AddMassTransit()
    .UseRabbitMQ();

builder.AddEFCore<AppDbContext>()
    .AutoMigrate()
    .UsePostgreSQL();

builder.AddRedisCache();

var app = await builder.BuildAsync();

app.MapServiceToHttpEndpoints<IMyAppService>();

await app.RunAsync();
```

## Architecture

```text
NOF.Domain                     <- Domain entities, aggregate roots, events
NOF.Contract                   <- Requests, commands, notifications, DTOs
NOF.Application                <- Handlers, state machines, application services
NOF.Hosting.Abstraction        <- Host builder and step contracts
NOF.Infrastructure             <- Builder implementation and shared runtime pipeline
NOF.UI                         <- Shared UI components and browser client primitives
NOF.Hosting.AspNetCore         <- ASP.NET Core host integration
NOF.Hosting.BlazorWebAssembly  <- Blazor WebAssembly host integration
NOF.Hosting.Maui               <- .NET MAUI host integration for cross-platform apps
NOF.Contract.Extension.*       <- Optional contract extensions (e.g., JWT)
NOF.Infrastructure.Extension.* <- Optional infrastructure extensions (e.g., JWT)
NOF.Infrastructure.*           <- Persistence, messaging, and caching providers
```

## Documentation

Full API documentation is available at the [GitHub Pages site](https://xmmshp.github.io/NOF/).

## Dependency Version Management

This repository uses Central Package Management via `Directory.Packages.props`.

- Root-level shared package versions are defined in `Directory.Packages.props`.
- `sample/Directory.Packages.props` imports the root file and should only declare sample-specific package versions.
- Avoid defining the same `<PackageVersion Include="...">` in both files, or NuGet restore will emit duplicate package version warnings (for example `NU1506`).

## License

This project is licensed under the [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0).
