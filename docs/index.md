# NOF Framework

NOF (**.NET Opinionated Framework**) is a convention-driven application framework for building modular, multi-tenant .NET applications.

## Key Features

- **Domain-Driven Design** — Aggregate roots, entities, domain events, and repositories out of the box.
- **CQRS & Messaging** — Commands, requests, notifications with transactional outbox support.
- **Multi-Tenancy** — Built-in tenant isolation at the database level with EF Core.
- **State Machines** — Declarative state machine definitions driven by notifications.
- **Source Generators** — Zero-reflection service registration, failure classes, HTTP endpoint mapping.
- **Infrastructure Adapters** — PostgreSQL, MassTransit (RabbitMQ), Redis, JWT authentication.

## Getting Started

Install the core packages from NuGet:

```bash
dotnet add package NOF.Hosting.AspNetCore
dotnet add package NOF.Infrastructure.EntityFrameworkCore.PostgreSQL
```

## API Reference

Browse the [API documentation](api/index.md) generated from XML doc comments.
