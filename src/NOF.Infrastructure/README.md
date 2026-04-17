# NOF.Infrastructure

Unified infrastructure entry package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

`NOF.Infrastructure` provides a single integration entry for infrastructure capabilities by composing:

- `NOF.Hosting.Abstraction`
- `NOF.Application`

This lets consumers reference one package/project while still getting the full default infrastructure setup.

## Usage

Add a single reference to `NOF.Infrastructure` in host or infrastructure-adapter projects.

## Installation

```shell
dotnet add package NOF.Infrastructure
```

## Built-in Capabilities

This package includes:

- In-memory cache (`MemoryCacheService`)
- In-memory riders (`MemoryCommandRider`, `MemoryNotificationRider`)
- In-process event publisher (`EventPublisher`)
- EF Core infrastructure primitives (`NOFDbContext`, outbox/inbox entities, tenant-aware model customization, `NOFDbContextFactory`)
- SQLite helpers for EF Core (`UseSqlite`, `UseSqliteInMemory`)

## EF Core

Built-in EF Core support includes:

- `NOFDbContext`
- outbox / inbox entities and background cleanup
- state machine context persistence
- tenant-aware model customization
- `NOFDbContextFactory`
- SQLite provider helpers

Example:

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaults: true);

builder.AddEFCore<AppDbContext>()
    .AutoMigrate();
```

## SQLite

`UseSqlite()` configures the `NOFDbContext` to use SQLite with the connection string resolved from your application configuration (default connection name: `"sqlite"`).

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaults: true);

builder.AddEFCore<AppDbContext>()
    .AutoMigrate()
    .UseSqlite();
```

### In-Memory SQLite

For tests or lightweight local scenarios, you can use SQLite's in-memory mode while still keeping relational behavior:

```csharp
builder.AddEFCore<AppDbContext>()
    .UseSingleTenant()
    .UseSqliteInMemory();
```

`UseSqliteInMemory()` keeps a named in-memory database alive across `DbContext` instances by holding an internal shared connection open for the process lifetime.

## License

Apache-2.0
