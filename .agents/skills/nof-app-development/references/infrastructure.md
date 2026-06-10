# NOF Infrastructure Setup Reference

## EF Core + PostgreSQL

```csharp
builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionString(builder.Configuration.GetConnectionString("postgres")
        ?? throw new InvalidOperationException("Connection string 'postgres' not found."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseNpgsql(connectionString))
    .MigrateOnInitialize();
```

Notes:
- `NOFDbContext` handles outbox, inbox, and state machine tables.
- Value object conversion is auto-wired for `IValueObject<T>`.
- Application code persists data through `DbContext` / `NOFDbContext`.
- Default in-memory cache state is isolated per host through DI singletons rather than process-wide mutable `static` fields.
- Ambient conveniences such as `Mapper`, `IdGenerator`, and `EventPublisher` are activated per scope; prefer explicit overloads when your app code benefits from visible dependencies.

## Redis Cache

```csharp
builder.AddRedisCache(builder.Configuration.GetConnectionString("redis")
    ?? throw new InvalidOperationException("Connection string 'redis' not found."));
```

Use `ICacheService` and `CacheKey<T>` for typed access.
The built-in memory cache keeps rider state and local lock state in DI singletons so multiple hosts in the same process do not bleed into each other.

## RabbitMQ

```csharp
builder.AddRabbitMQ(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
});
```

## JWT

Authority mode:

```csharp
builder.AddAuthenticationAuthority(o => o.Issuer = "MyApp");
```

Resource server mode:

```csharp
builder.AddAuthenticationResourceServer(o =>
{
    o.Issuer = "MyApp";
    o.JwksEndpoint = "http://localhost/.well-known/jwks.json";
    o.RequireHttpsMetadata = false;
});
```

Authority HTTP endpoints stay explicit:

```csharp
app.MapHttpEndpoint<TokenAuthorityService>();
app.MapGet("/.well-known/jwks.json", async (IJwksService jwksService, CancellationToken cancellationToken) =>
{
    var document = await jwksService.GetJwksAsync(cancellationToken);
    return Results.Ok(document);
});
```

## Configuration Snippet

```json
{
  "ConnectionStrings": {
    "postgres": "Host=localhost;Database=myapp;Username=postgres;Password=postgres",
    "redis": "localhost:6379",
    "rabbitmq": "Host=localhost;Port=5672;UserName=guest;Password=guest;VirtualHost=/"
  }
}
```
