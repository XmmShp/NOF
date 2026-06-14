# NOF Framework

**NOF** (short for **N**eat **O**pinionated **F**ramework) is a modular, convention-driven application framework for .NET that embraces clean architecture principles. It provides a structured approach to building scalable applications with built-in support for CQRS, domain-driven design, transactional outbox, state machines, multi-tenancy, and more.

## Key Features

- **Clean Architecture** - Layered packages (`Domain`, `Contract`, `Application`, `Infrastructure`) enforce separation of concerns.
- **CQRS & Messaging** - First-class `IRpcService`, command/notification dispatch, and handler pipelines.
- **Streaming RPC** - Contract-first server-streaming via `StreamingResult<T>` with HTTP SSE support in ASP.NET Core.
- **Source Generators** - Compile-time code generation for RPC servers, HTTP endpoint mapping, DI registration, failure definitions, and more.
- **Transactional Outbox** - Reliable message delivery with inbox/outbox pattern built into EF Core infrastructure.
- **State Machines** - Declarative, event-driven state machine builder with persistent context.
- **Multi-Tenancy** - Tenant-aware `DbContext` with database-per-tenant and shared-database modes.
- **Modular Pipeline** - Dependency-aware registration and initialization steps.
- **OpenTelemetry** - Built-in tracing, metrics, and logging integration.
- **Redis Cache** - Optional Redis-backed `ICacheService` via `NOF.Infrastructure.StackExchangeRedis`.
- **RabbitMQ Transport** - Optional command and notification transport via `NOF.Infrastructure.RabbitMQ`.
- **OIDC Server** - Optional authorization server endpoints with authorization code, refresh token, and client credentials grants.
- **UI & Test Helpers** - Blazor UI primitives and test-host utilities for application integration tests.

## Getting Started

Install the core packages from NuGet:

```bash
dotnet add package NOF.Hosting.AspNetCore
dotnet add package NOF.Infrastructure
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

Optional packages include:

```bash
dotnet add package NOF.Hosting.AspNetCore.Extension.OidcServer
dotnet add package NOF.Infrastructure.RabbitMQ
dotnet add package NOF.Infrastructure.StackExchangeRedis
dotnet add package NOF.UI
dotnet add package NOF.Test
```

## API Reference

Browse the [API documentation](api/index.md) generated from XML doc comments.
