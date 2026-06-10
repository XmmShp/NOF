# NOF Framework - AI Assistant Instructions

> **See also**: The `.agents/` directory contains organized rules, workflows, and skills for AI agents.

You are working in the **NOF (Neat Opinionated Framework)** repository, a modular .NET framework for building scalable applications with clean architecture, CQRS, and source generators.

## Repository Structure

```text
src/
  NOF.Abstraction/            - shared annotations and ambient event publishing primitives
  NOF.Domain/                 - value objects, failures, domain exceptions, and ID generation
  NOF.Contract/               - RPC contracts, request/response models, and HTTP endpoint metadata
  NOF.Application/            - RPC servers, handlers, state machines, mapping, and caching abstractions
  NOF.Hosting.Abstraction/    - builder contracts and step contracts
  NOF.Infrastructure/         - builder defaults, EF Core integration, OpenTelemetry, and runtime pipeline
  NOF.UI/                     - reusable UI primitives for Blazor-based clients
  NOF.Test/                   - lightweight test host and scoped test helpers for NOF apps
  Hostings/
    NOF.Hosting.AspNetCore/   - ASP.NET Core host integration and endpoint mapping
    NOF.Hosting.BlazorWebAssembly/ - Blazor WebAssembly host integration
    NOF.Hosting.Console/      - console host integration
    NOF.Hosting.Maui/         - MAUI host integration
  Extensions/
    NOF.Contract.Extension.Authentication/
    NOF.Hosting.Extension.Authentication/
    NOF.Infrastructure.Extension.Authentication/
  Infrastructures/
    NOF.Infrastructure.RabbitMQ/
    NOF.Infrastructure.StackExchangeRedis/
sample/                       - runnable sample app demonstrating current usage
tests/                        - unit and integration tests
```

## Key Patterns

- RPC contracts are declared as `IRpcService` interfaces with one request parameter per method.
- Application implementations use `RpcServer<TService>` and generated nested handler base classes.
- ASP.NET Core maps endpoints explicitly with `app.MapHttpEndpoint<TRpcServer>()`.
- `NOFWebApplicationBuilder.Create(args)` always registers JSON, CORS, and OpenAPI services.
- `app.MapOpenApi()` is an explicit host choice.
- EF Core registration uses `UseDbContext<TDbContext>()` plus `WithTenantMode(...)`, `WithConnectionString(...)`, `WithOptions(...)`, and optional `MigrateOnInitialize()`.
- Persist application data through `DbContext` / `NOFDbContext` and call `SaveChangesAsync()`; there is no public `_uow` abstraction.
- `Registry` is a first-class builder property; do not describe or implement it as hidden `Properties` state.
- Auto-injected services are recorded as native `ServiceDescriptor` entries in `Registry.AutoInjectRegistry`.
- `TypeResolver` is a DI singleton; do not reintroduce `TypeRegistry` or other process-wide type maps.
- `Mapper`, `IdGenerator`, and `EventPublisher` use ambient async-flow scope for convenience plus explicit overloads for full functionality.
- Avoid reintroducing process-wide mutable `static` state when a builder-level or DI singleton service can hold the same data.
- Central package versions live in `Directory.Packages.props`.

## Build and Validation

```bash
dotnet restore
dotnet build --configuration Release
dotnet test
dotnet format --verify-no-changes
```

## Documentation Sync

When framework behavior changes, check all of the following and keep them consistent with `src/`, `tests/`, and the sample app:

- `README.md`
- `docs/`
- `.agents/`
- `.github/`
- sample usage in `sample/`
