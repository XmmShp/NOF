# NOF Framework - AI Assistant Instructions

> **See also**: The `.agents/` directory contains organized rules, workflows, and skills for AI agents.

You are working in the **NOF (Neat Opinionated Framework)** repository, a modular .NET framework for building scalable applications with clean architecture, CQRS, and source generators.

## Repository Structure

```text
src/
  NOF.Abstraction/            - shared annotations and ambient event publishing primitives
  NOF.Domain/                 - aggregate roots, repositories, value objects, failures
  NOF.Contract/               - RPC contracts, commands, notifications, HTTP endpoint metadata
  NOF.Application/            - RPC servers, handlers, state machines, mapping, caching abstractions
  NOF.Hosting.Abstraction/    - builder contracts and step contracts
  NOF.Infrastructure/         - builder defaults, EF Core integration, OpenTelemetry, runtime pipeline
  NOF.UI/                     - reusable UI primitives for Blazor-based clients
  Hostings/
    NOF.Hosting.AspNetCore/   - ASP.NET Core host integration and endpoint mapping
    NOF.Hosting.BlazorWebAssembly/ - Blazor WebAssembly host integration
    NOF.Hosting.Console/      - console host integration
    NOF.Hosting.Maui/         - MAUI host integration
  Extensions/
    NOF.Contract.Extension.Authorization.Jwt/
    NOF.Hosting.Extension.Authorization.Jwt/
    NOF.Infrastructure.Extension.Authorization.Jwt/
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
- Aggregate mutations require `_uow.Update(entity)` before `SaveChangesAsync()`.
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
