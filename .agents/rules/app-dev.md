---
trigger: always_on
---

# NOF Application Development Rules

Use this file when building applications on top of NOF.

## Architecture

```text
MyApp.Domain/      entities, aggregate roots, value objects, domain events, repository interfaces
MyApp.Contract/    requests, commands, notifications, DTOs, failures
MyApp.Application/ handlers, event handlers, state machines, cache keys
MyApp/             host project (Program.cs, DbContext, migrations, appsettings)
```

Dependency direction: `Host -> Application -> Domain`, `Host -> Contract`, `Application -> Contract`.

## Core Abstractions

- `IRpcService`, `ICommand`, `INotification`, `IEvent`
- generated service implementation base types, `ICommandHandler`, `INotificationHandler`, `IEventHandler`
- `IRepository<T, TKey>`, `IUnitOfWork`
- generated HTTP/RPC service clients, `ICommandSender`, `INotificationPublisher`
- `IDeferredNotificationPublisher`, `IDeferredCommandSender`
- `CacheKey<T>`, `IMapper`, `Result` / `Result<T>`

## Source Generator Surface

- `IValueObject<T>` and `[NewableValueObject]`
- `[AutoInject]`
- `[Failure]`
- `[PublicApi]`, `[HttpEndpoint]`, `[GenerateService]`
- `[Mappable<TSource, TDestination>]`

## Program.cs Baseline

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaults: true);

builder.AddApplicationPart(typeof(MyAppService).Assembly);

builder.AddRedisCache();
builder.AddJwtAuthority(o => o.Issuer = "MyApp");
builder.AddJwtResourceServer(o =>
{
    o.Issuer = "MyApp";
    o.RequireHttpsMetadata = false;
    o.JwksEndpoint = "http://localhost/.well-known/jwks.json";
});
builder.AddRabbitMQ();
builder.AddEFCore<AppDbContext>().AutoMigrate().UsePostgreSQL();

var app = await builder.BuildAsync();
app.MapServiceToHttpEndpoints<IMyAppService>();
await app.RunAsync();
```

## JWT Usage Notes

- Authority mode: `AddJwtAuthority(...)`.
- Resource server mode: `AddJwtResourceServer(...)`.
- In handlers, use `IUserContext` and `IExecutionContext` to access user, tenant, and tracing info.

## Important Convention

After modifying aggregate roots, always call `_uow.Update(entity)` before `SaveChangesAsync()`.

## Workflows

Use `.agents/workflows/app-dev/*` for step-by-step guides, especially:
- `scaffold-nof-app.md`
- `add-request-handler.md`
- `add-jwt-auth.md`
- `add-rabbitmq-messaging.md`
- `add-redis-caching.md`
