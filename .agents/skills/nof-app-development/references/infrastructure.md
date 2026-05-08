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

## Redis Cache

```csharp
builder.AddRedisCache(builder.Configuration.GetConnectionString("redis"));
```

Use `ICacheService` and `CacheKey<T>` for typed access.

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

Authority HTTP endpoints stay explicit:

```csharp
app.MapHttpEndpoint<JwtAuthorityService>();
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
