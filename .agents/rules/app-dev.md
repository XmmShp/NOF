---
trigger: always_on
---

# NOF Application Development Rules

Use this file when building applications on top of NOF.

## Architecture

```text
MyApp.Domain/      domain classes, value objects, failures, in-memory event payloads
MyApp.Contract/    RPC contracts, request/response models, commands, notifications, DTOs
MyApp.Application/ RPC servers and handlers, event handlers, mappings, cache keys
MyApp/             host, concrete persistence provider, Program.cs, appsettings
```

Typical dependency direction is `Host -> Application`, `Application -> Domain + Contract`, with the host selecting infrastructure and hosting packages.

## Core Abstractions

- RPC: `IRpcService`, `RpcServer<TService>`, generated nested RPC handler bases, generated client interfaces and implementations.
- Messaging: `CommandHandler<T>`, `NotificationHandler<T>`, `ICommandSender`, `INotificationPublisher`.
- In-process events: `InMemoryEventHandler<T>`, `IEventPublisher`, `PublishAsEvent()`.
- Persistence: `IDbContext`, `IDbContextFactory`, `IRepository<T>`, provider-neutral async LINQ extensions.
- Caching and mapping: `ICacheService`, `CacheKey<T>`, `IMapper`, `ProjectTo<TDestination>()`.
- Results and request models: `Result`, `Result<T>`, `StreamingResult<T>`, `Optional<T>`.

Commands and notifications are plain payload objects; inheriting the corresponding handler base opts a handler into generated registration.

## RPC Rules

- Every RPC contract inherits `IRpcService` and declares exactly one transport: `[TransportOverHttp(...)]` or `[TransportOverMemory]`.
- A contract method takes exactly one reference-type request and returns a non-task `IResult` implementation. Do not put `CancellationToken` on the contract method or suffix it with `Async`.
- Controller-style HTTP methods use `[HttpEndpoint]`; JSON-RPC and memory contracts must not.
- A generated server container is a `partial` class inheriting `RpcServer<TService>`.
- Register each server with `builder.AddRpcServer<TServer>()`; the host maps applicable transports during `BuildAsync()`.
- Generated client and server handler methods are asynchronous and receive `Context` explicitly.

## Program.cs Baseline

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Hosting;
using NOF.Hosting.AspNetCore;
using NOF.Infrastructure.EntityFrameworkCore;

var builder = NOFWebApplicationBuilder.Create(args);

builder.AddApplicationPart(typeof(OrderService).Assembly);
builder.AddRpcServer<OrderService>();

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

Add Redis and RabbitMQ through `builder.Services.AddRedisCache(...)` and `builder.Services.AddRabbitMQ(...)`. `NOFWebApplicationBuilder.Create(args)` already registers the default in-memory persistence, cache, command rider, and notification rider; explicit providers replace those defaults.

## Persistence Convention

Application code depends on `IDbContext` and `IRepository<T>`, not EF Core `DbContext`. The host owns `NOFDbContext`, EF model configuration, migrations, and the call to `UseDbContext<T>()`.

Deferred messages are added to the current `IDbContext` and become transactional only when the same context saves successfully:

```csharp
await notificationPublisher.DeferPublishAsync(notification, context, cancellationToken);
await dbContext.SaveChangesAsync(cancellationToken);
```

## Authentication

- Register a local OAuth/OIDC authority with `builder.AddOidcServer(...)` from `NOF.Hosting.AspNetCore.Extension.OidcServer`; its endpoints are mapped automatically.
- Register JWT validation with `builder.Services.AddAuthenticationResourceServer(...)`, configuring `AuthorizationServerIssuer`, `ExpectedIssuer`, and optional `Audience`.
- Read identity through `IUserContext`; read the normalized current tenant through `ICurrentTenant`.
- OIDC persistence uses the configured `IDbContext`, so configure a durable provider and migrations for production.

## Source Generators and Conventions

- Construct `IValueObject<T>` values through generated `Of(...)` / `New(...)`; never use `default` or parameterless `new()` (`NOF018`).
- Use `[ValueObjectLength]` on string-backed value objects instead of duplicating maximum lengths in persistence mapping.
- Declare mappings on a `partial static` class with `[Mappable<TSource, TDestination>]`.
- Filter, order, and page before `ProjectTo<TDestination>()`; `NOF025` reports server-side shaping after projection.
- Add every assembly containing generated initializers with `AddApplicationPart(...)`.
