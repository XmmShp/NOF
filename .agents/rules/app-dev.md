---
trigger: always_on
---

# NOF Application Development Rules

Use this file when building applications on top of NOF.

## Architecture

```text
MyApp.Domain/      domain classes, value objects, failures, domain event payloads
MyApp.Contract/    RPC contracts, request/response models, DTOs, failures
MyApp.Application/ RPC servers, request handlers, command handlers, notification handlers, state machines
MyApp/             host project (Program.cs, DbContext, appsettings)
```

Dependency direction: `Host -> Application -> Domain`, `Host -> Contract`, `Application -> Contract`.

## Core Abstractions

- `IRpcService`
- `RpcServer<TService>` and generated nested RPC handler bases
- `CommandHandler<T>`, `NotificationHandler<T>`, `InMemoryEventHandler<T>`
- `ICommandSender`, `INotificationPublisher`, `IEventPublisher`
- `CacheKey<T>`, `IMapper`, `Result` / `Result<T>`
- `DbContext` / `NOFDbContext` for persistence
- Commands and notifications are plain payload objects; use handler base types to opt into dispatch.

## Source Generator Surface

- `IValueObject<T>` and `[NewableValueObject]`
- `[AutoInject]`
- `[Failure]`
- `[HttpEndpoint]`
- `[Mappable<TSource, TDestination>]`

## Program.cs Baseline

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Hosting.AspNetCore;
using NOF.Infrastructure;
using NOF.Infrastructure.Extension.Authentication;
using NOF.Infrastructure.RabbitMQ;
using NOF.Infrastructure.StackExchangeRedis;

var builder = NOFWebApplicationBuilder.Create(args);

builder.AddApplicationPart(typeof(MyAppService).Assembly);

builder.AddRedisCache(builder.Configuration.GetConnectionString("redis")
    ?? throw new InvalidOperationException("Connection string 'redis' not found."));
builder.AddAuthenticationAuthority(o =>
{
    o.Issuer = "MyApp";
    o.SigningKeyEncryptionKey = builder.Configuration["NOF:Authority:SigningKeyEncryptionKey"]
        ?? throw new InvalidOperationException("Configuration value 'NOF:Authority:SigningKeyEncryptionKey' not found.");
});
builder.AddAuthenticationResourceServer(o =>
{
    o.Issuer = "MyApp";
    o.RequireHttpsMetadata = false;
    o.JwksEndpoint = "http://localhost/.well-known/jwks.json";
});
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

## JWT Usage Notes

- Authority mode: `AddAuthenticationAuthority(...)`.
- Resource server mode: `AddAuthenticationResourceServer(...)`.
- `AddAuthenticationAuthority(...)` adds the authority assembly as an application part for you.
- Expose authority HTTP endpoints explicitly with `app.MapHttpEndpoint<TokenAuthorityService>()` and a JWKS endpoint when needed.

## Important Convention

Persist application data through `DbContext` / `NOFDbContext` in the application layer. Do not rely on nonexistent public repository or unit-of-work abstractions.

## Workflows

Use `.agents/workflows/app-dev/*` for step-by-step guides, especially:
- `scaffold-nof-app.md`
- `add-request-handler.md`
- `add-jwt-auth.md`
- `add-rabbitmq-messaging.md`
- `add-redis-caching.md`
