# NOF.Infrastructure

Unified infrastructure entry package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

`NOF.Infrastructure` provides the default runtime wiring for NOF applications, including:

- builder defaults and step orchestration
- in-memory cache and messaging riders
- EF Core integration through `UseDbContext<TDbContext>()`
- OpenTelemetry registration and transport middleware
- tenant-aware `NOFDbContext` support
- builder-scoped `TypeResolver`
- ambient `IMapper` / `IIdGenerator` activation through scoped `IDaemonService`

This lets consumers reference one package/project while still getting the full default infrastructure setup.

## Usage

Add a single reference to `NOF.Infrastructure` in host or infrastructure-adapter projects.

## Installation

```shell
dotnet add package NOF.Infrastructure
```

## Built-in Capabilities

This package includes:

- in-memory cache (`ICacheService` + `MemoryCacheServiceRider`)
- in-memory riders (`MemoryCommandRider`, `MemoryNotificationRider`)
- in-process event publisher (`IEventPublisher`)
- EF Core infrastructure primitives (`NOFDbContext`, outbox/inbox entities, tenant-aware model customization, `NOFDbContextFactory`)
- SQLite-based default persistence used by infrastructure defaults

The default in-memory cache implementation is isolated per NOF host:

- cache data lives in `MemoryCacheServiceRiderState`
- local `GetOrSetAsync(...)` locks live in `CacheServiceLocalLockState`
- both are registered as DI singletons instead of process-wide `static` state

## EF Core

Built-in EF Core support is configured through `UseDbContext<TDbContext>()` and `EFCoreSelector`:

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Infrastructure;

var builder = NOFWebApplicationBuilder.Create(args);

builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionString(builder.Configuration.GetConnectionString("postgres")
        ?? throw new InvalidOperationException("Connection string 'postgres' not found."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseNpgsql(connectionString))
    .MigrateOnInitialize();
```

Available configuration methods:

- `WithTenantMode(...)`
- `WithConnectionString(...)`
- `WithOptions(...)`
- `WithModelCreating(...)`
- `MigrateOnInitialize()`

## SQLite

For SQLite, provide the provider configuration via `WithOptions(...)`:

```csharp
builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.SharedDatabase)
    .WithConnectionString(builder.Configuration.GetConnectionString("sqlite")
        ?? throw new InvalidOperationException("Connection string 'sqlite' not found."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseSqlite(connectionString))
    .MigrateOnInitialize();
```

## In-Memory SQLite

For tests or lightweight local scenarios, you can use the built-in default SQLite memory configuration that ships with `AddInfrastructureDefaults()`.
If you need a custom app `DbContext`, configure it explicitly with `UseDbContext<TDbContext>()`.

## License

Apache-2.0
