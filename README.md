# NOF Framework

[![CI](https://github.com/XmmShp/NOF/actions/workflows/ci.yml/badge.svg)](https://github.com/XmmShp/NOF/actions/workflows/ci.yml)
[![CD](https://github.com/XmmShp/NOF/actions/workflows/cd.yml/badge.svg)](https://github.com/XmmShp/NOF/actions/workflows/cd.yml)

**NOF** (short for **N**eat **O**pinionated **F**ramework) is a modular, convention-driven application framework for .NET that embraces clean architecture principles. It provides a structured approach to building scalable applications with built-in support for CQRS, transactional outbox, state machines, multi-tenancy, and observability.

## Features

- **Clean Architecture** - layered packages (`Domain`, `Contract`, `Application`, `Infrastructure`) enforce separation of concerns
- **CQRS & Messaging** - first-class `IRpcService`, typed command/notification dispatch, and handler pipelines
- **Source Generators** - compile-time code generation for RPC servers, HTTP endpoint mapping, DI registration, mapping registration, and failure definitions
- **Transactional Outbox** - reliable message delivery with inbox/outbox pattern built into EF Core infrastructure
- **State Machines** - declarative, event-driven state machine builder with persistent context
- **Multi-Tenancy** - tenant-aware persistence and deployment-aware runtime defaults
- **Modular Pipeline** - dependency-aware registration and initialization steps
- **OpenTelemetry** - built-in tracing, metrics, and logging integration

## Packages

| Package                                                                                                                           | Description                                                                                                             |
| --------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| [`NOF.Abstraction`](https://www.nuget.org/packages/NOF.Abstraction)                                                               | Shared annotations, user context, ambient headers, and in-memory event primitives                                       |
| [`NOF.Domain`](https://www.nuget.org/packages/NOF.Domain)                                                                         | Domain primitives - value objects, failures, domain exceptions, and ID generation                                       |
| [`NOF.Contract`](https://www.nuget.org/packages/NOF.Contract)                                                                     | Contract layer - RPC contracts, request/response models, `Result<T>`, `Optional<T>`, HTTP endpoint attributes           |
| [`NOF.Application`](https://www.nuget.org/packages/NOF.Application)                                                               | Application layer - RPC servers, handlers, state machines, mapper abstractions, and caching contracts                   |
| [`NOF.Hosting.Abstraction`](https://www.nuget.org/packages/NOF.Hosting.Abstraction)                                               | Hosting abstractions - builder contracts, step contracts, and dependency ordering                                       |
| [`NOF.Infrastructure`](https://www.nuget.org/packages/NOF.Infrastructure)                                                         | Core infrastructure - builder defaults, EF Core integration, transactional messaging, OpenTelemetry, and runtime wiring |
| [`NOF.UI`](https://www.nuget.org/packages/NOF.UI)                                                                                 | Reusable UI primitives - authorization components, browser storage, browser info, and Blazor client helpers             |
| [`NOF.Hosting.AspNetCore`](https://www.nuget.org/packages/NOF.Hosting.AspNetCore)                                                 | ASP.NET Core hosting - middleware, OpenAPI registration, service endpoint mapping, and JSON configuration               |
| [`NOF.Hosting.BlazorWebAssembly`](https://www.nuget.org/packages/NOF.Hosting.BlazorWebAssembly)                                   | Blazor WebAssembly hosting - host builder integration for browser apps                                                  |
| [`NOF.Hosting.Console`](https://www.nuget.org/packages/NOF.Hosting.Console)                                                       | Console hosting - Microsoft.Extensions.Hosting integration with the NOF pipeline                                        |
| [`NOF.Hosting.Maui`](https://www.nuget.org/packages/NOF.Hosting.Maui)                                                             | .NET MAUI hosting - MAUI app builder integration for cross-platform applications                                        |
| [`NOF.Contract.Extension.Authorization.Jwt`](https://www.nuget.org/packages/NOF.Contract.Extension.Authorization.Jwt)             | JWT contract extension - authority service contracts and token models                                                   |
| [`NOF.Hosting.Extension.Authorization.Jwt`](https://www.nuget.org/packages/NOF.Hosting.Extension.Authorization.Jwt)               | JWT outbound propagation - writes bearer tokens to outbound NOF requests                                                |
| [`NOF.Infrastructure.Extension.Authorization.Jwt`](https://www.nuget.org/packages/NOF.Infrastructure.Extension.Authorization.Jwt) | JWT authority and resource server integration - token issuance, validation, key rotation, and JWKS                      |
| [`NOF.Infrastructure.RabbitMQ`](https://www.nuget.org/packages/NOF.Infrastructure.RabbitMQ)                                       | RabbitMQ transport for NOF command and notification dispatch                                                            |
| [`NOF.Infrastructure.StackExchangeRedis`](https://www.nuget.org/packages/NOF.Infrastructure.StackExchangeRedis)                   | Redis-backed cache infrastructure via StackExchange.Redis                                                               |
| [`NOF.Test`](https://www.nuget.org/packages/NOF.Test)                                                                             | Test host helpers for NOF applications                                                                                  |

## Quick Start

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Hosting.AspNetCore;
using NOF.Infrastructure;
using NOF.Infrastructure.RabbitMQ;
using NOF.Infrastructure.StackExchangeRedis;

var builder = NOFWebApplicationBuilder.Create(args);

builder.AddApplicationPart(typeof(MyAppService).Assembly);

builder.AddRedisCache(builder.Configuration.GetConnectionString("redis")
    ?? throw new InvalidOperationException("Connection string 'redis' not found."));
builder.AddRabbitMQ(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
});

builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionString(builder.Configuration.GetConnectionString("postgres")
        ?? throw new InvalidOperationException("Connection string 'postgres' not found."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseNpgsql(connectionString))
    .MigrateOnInitialize();

var app = await builder.BuildAsync();

app.MapOpenApi();
app.MapHttpEndpoint<MyAppService>();

await app.RunAsync();
```

## Architecture

```text
NOF.Abstraction                <- Shared annotations, user context, in-memory events
NOF.Domain                     <- Value objects, failures, domain utilities
NOF.Contract                   <- RPC contracts, payload models, endpoint metadata
NOF.Application                <- RPC servers, handlers, state machines, mapping, caching
NOF.Hosting.Abstraction        <- Host builder and step contracts
NOF.Infrastructure             <- Builder defaults and shared runtime pipeline
NOF.UI                         <- Shared UI components and browser client primitives
NOF.Test                       <- Test host helpers for application and integration testing
NOF.Hosting.AspNetCore         <- ASP.NET Core host integration
NOF.Hosting.BlazorWebAssembly  <- Blazor WebAssembly host integration
NOF.Hosting.Console            <- Console host integration
NOF.Hosting.Maui               <- .NET MAUI host integration
NOF.Contract.Extension.*       <- Optional contract extensions (for example JWT)
NOF.Hosting.Extension.*        <- Optional hosting extensions (for example outbound JWT propagation)
NOF.Infrastructure.Extension.* <- Optional infrastructure extensions (for example JWT authority/resource server)
NOF.Infrastructure.*           <- Messaging and caching providers
```

## Documentation

Full API documentation is available at the [GitHub Pages site](https://xmmshp.github.io/NOF/).

## JSON And AOT

NOF supports both regular JIT execution and Native AOT-oriented setups.

- Framework-managed HTTP JSON paths use `JsonTypeInfo`/`JsonSerializerContext`-based APIs where possible.
- `JsonSerializerOptions.NOF` ships with built-in metadata for NOF's common primitive needs and value-object primitives.
- Application DTOs still need source-generated metadata in AOT scenarios.

Register your app context before the type is first serialized or deserialized:

```csharp
using System.Text.Json;
using MyApp;

JsonSerializerOptions.ConfigureNOFJsonSerializerOptions(options =>
{
    options.TypeInfoResolverChain.Add(MyAppJsonSerializerContext.Default);
});
```

If metadata is missing, NOF now throws a framework-specific error that includes the concrete runtime type name to make AOT failures easier to locate.

## Testing

The test layout is organized by package category:

- Core and host package tests: `tests/NOF.*.Tests`
- Extension package tests: `tests/Extensions/NOF.*.Tests`
- Infrastructure provider tests: `tests/Infrastructures/NOF.*.Tests`
- Shared test utilities: `tests/Common/*`
- Source generator tests are colocated with parent package tests when practical

## Dependency Version Management

This repository uses Central Package Management via `Directory.Packages.props`.

- Root-level shared package versions are defined in `Directory.Packages.props`.
- `sample/Directory.Packages.props` imports the root file and should only declare sample-specific package versions.
- Avoid defining the same `<PackageVersion Include="...">` in both files, or NuGet restore will emit duplicate package version warnings (for example `NU1506`).

## License

This project is licensed under the [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0).
