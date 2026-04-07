# NOF Infrastructure Setup Reference

## EF Core + PostgreSQL

```csharp
builder.AddEFCore<AppDbContext>()
    .UseSharedDatabaseTenancy()
    .AutoMigrate()
    .UsePostgreSQL();
```

Notes:
- `NOFDbContext` handles outbox/inbox/state machine tables.
- `ChangeTracker.AutoDetectChangesEnabled` is disabled; call `_uow.Update(entity)` explicitly.
- Value object conversion is auto-wired for `IValueObject<T>`.

## Redis Cache

```csharp
builder.AddRedisCache();
```

Use `ICacheService` and `CacheKey<T>` for typed access.

## RabbitMQ

```csharp
builder.AddRabbitMQ();
```

Send through abstractions:
- generated RPC service clients
- `ICommandSender`
- `INotificationPublisher`

## JWT

Authority mode:

```csharp
builder.AddJwtAuthority(o => o.Issuer = "MyApp");
```

Resource server mode:

```csharp
builder.AddJwtResourceServer(o =>
{
    o.Issuer = "MyApp";
    o.JwksEndpoint = "http://localhost/.well-known/jwks.json";
    o.RequireHttpsMetadata = false;
});
```

Map service contracts:

```csharp
app.MapServiceToHttpEndpoints<IJwtAuthorityService>();
app.MapServiceToHttpEndpoints<IJwksService>();
```

## Configuration Snippet

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=myapp;Username=postgres;Password=postgres",
    "redis": "localhost:6379",
    "rabbitmq": "Host=localhost;Port=5672;UserName=guest;Password=guest;VirtualHost=/"
  }
}
```
