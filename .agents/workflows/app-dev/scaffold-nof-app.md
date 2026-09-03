---
description: Scaffold a minimal NOF ASP.NET Core application with clean layers, generated RPC, and the EF Core provider
---

# Scaffold a New NOF Web Application

## Project Structure

```text
MyApp/
  MyApp/                    host, Program.cs, AppDbContext, appsettings.json
  MyApp.Domain/             domain classes, value objects, failures, events
  MyApp.Application/        RPC servers and handlers, mappings, cache keys
  MyApp.Contract/           RPC contracts, messages, DTOs
```

## 1. Create the Solution and Projects

```bash
mkdir MyApp
cd MyApp
dotnet new sln -n MyApp
dotnet new web -n MyApp -o MyApp
dotnet new classlib -n MyApp.Domain -o MyApp.Domain
dotnet new classlib -n MyApp.Application -o MyApp.Application
dotnet new classlib -n MyApp.Contract -o MyApp.Contract
dotnet sln add MyApp/MyApp.csproj MyApp.Domain/MyApp.Domain.csproj MyApp.Application/MyApp.Application.csproj MyApp.Contract/MyApp.Contract.csproj
```

## 2. Add Packages

```bash
dotnet add MyApp/MyApp.csproj package NOF.Hosting.AspNetCore
dotnet add MyApp/MyApp.csproj package NOF.Infrastructure.EntityFrameworkCore
dotnet add MyApp/MyApp.csproj package Npgsql.EntityFrameworkCore.PostgreSQL

dotnet add MyApp.Domain/MyApp.Domain.csproj package NOF.Domain
dotnet add MyApp.Contract/MyApp.Contract.csproj package NOF.Contract
dotnet add MyApp.Application/MyApp.Application.csproj package NOF.Application
```

The runtime packages carry their layer-specific analyzers/source generators in the NuGet package.

## 3. Add Project References

```bash
dotnet add MyApp.Application/MyApp.Application.csproj reference MyApp.Domain/MyApp.Domain.csproj MyApp.Contract/MyApp.Contract.csproj
dotnet add MyApp/MyApp.csproj reference MyApp.Application/MyApp.Application.csproj
```

## 4. Define a Contract

Create `MyApp.Contract/IAppService.cs`:

```csharp
using NOF.Contract;

namespace MyApp.Contract;

[TransportOverHttp(HttpRpcStyle.ControllerRpc, "api")]
public interface IAppService : IRpcService
{
    [HttpEndpoint(HttpVerb.Get, "health")]
    Result<HealthResponse> Health(Empty request);
}

public sealed record HealthResponse(string Status);
```

## 5. Implement the RPC Server

Create `MyApp.Application/AppService.cs`:

```csharp
using NOF.Application;
using NOF.Contract;
using MyApp.Contract;

namespace MyApp.Application;

public partial class AppService : RpcServer<IAppService>;

public sealed class Health : AppService.Health
{
    public override Task<Result<HealthResponse>> HandleAsync(
        Empty request,
        Context context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new HealthResponse("ok")));
    }
}
```

## 6. Add the Host DbContext

Create `MyApp/AppDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Infrastructure.EntityFrameworkCore;

namespace MyApp;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : NOFDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
```

## 7. Configure Program.cs

```csharp
using Microsoft.EntityFrameworkCore;
using MyApp;
using MyApp.Application;
using NOF.Hosting;
using NOF.Hosting.AspNetCore;
using NOF.Infrastructure.EntityFrameworkCore;

var builder = NOFWebApplicationBuilder.Create(args);

builder.AddApplicationPart(typeof(AppService).Assembly);
builder.AddRpcServer<AppService>();

builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionString(builder.Configuration.GetConnectionString("postgres")
        ?? throw new InvalidOperationException("Connection string 'postgres' not found."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseNpgsql(connectionString))
    .MigrateOnInitialize();

var app = await builder.BuildAsync();
app.MapOpenApi();
await app.RunAsync();
```

## 8. Configure appsettings.json

```json
{
  "ConnectionStrings": {
    "postgres": "Host=localhost;Database=myapp;Username=postgres;Password=postgres"
  }
}
```

## Notes

- `NOFWebApplicationBuilder.Create(args)` registers NOF infrastructure, JSON/OpenAPI services, health endpoints, and in-memory defaults.
- `AddApplicationPart(...)` executes generated assembly initializers; `AddRpcServer<T>()` explicitly registers the RPC server.
- Applicable RPC endpoints are mapped automatically during `BuildAsync()`.
- Add RabbitMQ, Redis, or OIDC packages only when the application needs those providers.
- Application handlers should inject `IDbContext` / `IRepository<T>`; keep EF Core types in the host.
