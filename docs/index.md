# NOF Framework

**NOF** (short for **N**eat **O**pinionated **F**ramework) is a modular, convention-driven application framework for .NET that embraces clean architecture principles. It provides a structured approach to building scalable applications with built-in support for CQRS, domain-driven design, transactional outbox, state machines, multi-tenancy, and more.

## Key Features

- **Clean Architecture** — Layered packages (`Domain`, `Contract`, `Application`, `Infrastructure`) enforce separation of concerns.
- **CQRS & Messaging** — First-class `IRequest`, `ICommand`, `INotification` abstractions with handler pipelines.
- **Source Generators** — Compile-time code generation for HTTP endpoint mapping, DI registration, failure definitions, and more.
- **Transactional Outbox** — Reliable message delivery with inbox/outbox pattern built into EF Core infrastructure.
- **State Machines** — Declarative, event-driven state machine builder with persistent context.
- **Multi-Tenancy** — Tenant-aware `DbContext` with automatic model filtering and migration isolation.
- **Modular Pipeline** — Dependency-aware `IStep` system for ordered service registration and application initialization.
- **OpenTelemetry** — Built-in tracing, metrics, and logging integration.

## Getting Started

Install the core packages from NuGet:

```bash
dotnet add package NOF.Hosting.AspNetCore
dotnet add package NOF.Infrastructure.EntityFrameworkCore.PostgreSQL
```

## API Reference

Browse the [API documentation](api/index.md) generated from XML doc comments.
