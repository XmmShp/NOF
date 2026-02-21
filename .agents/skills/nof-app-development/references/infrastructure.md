# NOF Infrastructure Setup

## Table of Contents

- [EF Core + PostgreSQL](#ef-core--postgresql)
- [Redis Caching](#redis-caching)
- [MassTransit + RabbitMQ](#masstransit--rabbitmq)
- [JWT Authentication](#jwt-authentication)
- [Configuration Reference](#configuration-reference)

---

## EF Core + PostgreSQL

### Register

```csharp
builder.AddEFCore<AppDbContext>().AutoMigrate().UsePostgreSQL();
```

### Migrations

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project MyApp --context AppDbContext
dotnet ef database update --project MyApp --context AppDbContext
```

Key points:
- `NOFDbContext` auto-configures outbox/inbox/state machine tables
- `AutoMigrate()` applies pending migrations on startup (dev only)
- `IUnitOfWork` is registered as `EFCoreUnitOfWork` by `AddEFCore<T>()`
- **Value objects are automatically handled** — `ValueObjectValueConverterSelector` detects all `[ValueObject<T>]` types and provides EF Core `ValueConverter` instances at runtime. No manual converter registration needed.

---

## Redis Caching

### Register

```csharp
builder.AddRedisCache();
```

### ICacheService API

| Method | Description |
|--------|-------------|
| `GetAsync<T>(key, ct)` | Returns `Optional<T>` — check `.HasValue` |
| `GetOrSetAsync<T>(key, factory, options, ct)` | Get or compute and cache |
| `SetAsync<T>(key, value, options, ct)` | Store a value |
| `RemoveAsync(key, ct)` | Remove a cached entry |
| `ExistsAsync(key, ct)` | Check if key exists |
| `GetManyAsync<T>(keys, ct)` | Batch get multiple keys |

Key points:
- Without Redis, NOF uses in-memory cache by default
- `CacheKey<T>` is strongly typed — generic `T` ensures compile-time safety
- `Optional<T>` distinguishes "not in cache" from "cached null"

---

## MassTransit + RabbitMQ

### Register

```csharp
builder.AddMassTransit().UseRabbitMQ();
```

### Cross-service messaging

```csharp
// Send command to specific service
await _commandSender.SendAsync(command, destinationEndpointName: "payment-service", cancellationToken: ct);

// Send request to specific service (RPC)
var result = await _requestSender.SendAsync(request, destinationEndpointName: "inventory-service", cancellationToken: ct);

// Broadcast notification to all subscribers
await _notificationPublisher.PublishAsync(notification, ct);
```

Key points:
- Headers (identity, tenant, tracing) propagate automatically across services
- Use `IRequestSender`, `ICommandSender`, `INotificationPublisher` — never use MassTransit APIs directly
- Use `IDeferredNotificationPublisher` / `IDeferredCommandSender` for outbox-based deferred dispatch
- Only `IRequestSender` and `ICommandSender` support `destinationEndpointName`; `INotificationPublisher` broadcasts to all subscribers
- Handler discovery is automatic via source generators

---

## JWT Authentication

### Authority mode (issue + validate tokens)

```csharp
builder.AddJwtAuthority().AddJwksRequestHandler();
builder.AddJwtAuthorization();
```

### Authorization-only mode (validate tokens from external authority)

```csharp
builder.AddJwtAuthorization();
// Or: builder.AddJwtAuthorization("https://auth.example.com/.well-known/jwks.json");
```

### Issue tokens

```csharp
var result = await _requestSender.SendAsync(new GenerateJwtTokenRequest
{
    Subject = userId,
    Claims = new Dictionary<string, string> { ["role"] = "admin" }
});
// result.Value.AccessToken, result.Value.RefreshToken
```

### Protect endpoints

All endpoints require authentication by default. Use `[AllowAnonymous]` to opt out.

Access identity via `IInvocationContext`:
- `_context.User` — `ClaimsPrincipal` (the current user)
- `_context.TenantId` — `string?` (tenant ID)
- `_context.TraceId` / `_context.SpanId` — distributed tracing

Key points:
- JWT keys auto-rotate via background service in Authority mode
- JWKS endpoint auto-exposed at `/.well-known/jwks.json`
- Authorization middleware works for both HTTP and MassTransit messages

---

## Configuration Reference

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=myapp;Username=postgres;Password=postgres",
    "redis": "localhost:6379",
    "rabbitmq": "amqp://guest:guest@localhost:5672"
  },
  "NOF": {
    "Jwt": {
      "Authority": {
        "Issuer": "https://myapp.example.com",
        "AccessTokenLifetime": "01:00:00",
        "RefreshTokenLifetime": "30.00:00:00",
        "KeyRotationInterval": "7.00:00:00"
      },
      "Authorization": {
        "Issuer": "https://auth.example.com",
        "JwksEndpoint": "https://auth.example.com/.well-known/jwks.json"
      }
    }
  }
}
```
