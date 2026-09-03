# NOF Infrastructure Setup Reference

Concrete providers are host concerns. Application handlers should continue to depend on `IDbContext`, `IRepository<T>`, `ICacheService`, `ICommandSender`, and `INotificationPublisher`.

## EF Core + PostgreSQL

Packages:

```bash
dotnet add package NOF.Infrastructure.EntityFrameworkCore
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

Registration:

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Hosting;
using NOF.Infrastructure.EntityFrameworkCore;

builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionString(builder.Configuration.GetConnectionString("postgres")
        ?? throw new InvalidOperationException("Connection string 'postgres' not found."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseNpgsql(connectionString))
    .MigrateOnInitialize();
```

`NOFDbContext` applies the registered model contributors for NOF tenant, inbox, outbox, and ordered-message entities. It also supplies value-object conversion/length conventions, multi-tenancy, and soft delete. Soft delete is enabled by default; use `.WithSoftDelete(false)` for a context-wide opt-out or `HasSoftDelete(...)` in EF model configuration for an entity override.

The host may inject EF `DbContext` or the concrete context, but application handlers should use `IDbContext` / `IRepository<T>` so they remain provider-neutral.

## Default In-Memory Infrastructure

`NOFWebApplicationBuilder.Create(args)` calls `AddNOFInfrastructure()`, which registers in-memory persistence, cache, command, notification, and backplane defaults. Use those defaults for lightweight development/tests; selecting a provider replaces the corresponding abstractions.

`builder.AddNOFEntityFrameworkCore()` is a separate EF Core SQLite in-memory option. It is useful when code needs EF-specific behavior rather than the provider-neutral in-memory store.

## Redis Cache and Backplane

Package:

```bash
dotnet add package NOF.Infrastructure.StackExchangeRedis
```

Registration:

```csharp
builder.Services.AddRedisCache(builder.Configuration.GetConnectionString("redis")
    ?? throw new InvalidOperationException("Connection string 'redis' not found."));
```

This replaces `ICacheServiceRider`; callers continue to inject `ICacheService`. Use `AddRedisBackplane(...)` separately when `IBackplane` should also use Redis.

## RabbitMQ

Package:

```bash
dotnet add package NOF.Infrastructure.RabbitMQ
```

Registration:

```csharp
builder.Services.AddRabbitMQ(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
});
```

This replaces the default `ICommandRider` and `INotificationRider` and adds the consumer hosted service. `AddRabbitMQBackplane(...)` is independent and replaces only `IBackplane`.

## OAuth/OIDC Authority

Package:

```bash
dotnet add package NOF.Hosting.AspNetCore.Extension.OidcServer
```

Registration:

```csharp
using NOF.Hosting.AspNetCore.Extension.OidcServer;

builder.AddOidcServer(options =>
{
    options.Issuer = "https://auth.example.com/oauth2";
    options.AccessTokenAudience = "my-app";
    options.SigningKeyEncryptionKey = builder.Configuration["NOF:OidcServer:SigningKeyEncryptionKey"]
        ?? throw new InvalidOperationException("OIDC signing-key encryption key not found.");
})
.AddPublicClient(
    "my-app-ui",
    ["openid", "profile", "my-app.read"],
    redirectUris: ["https://app.example.com/oauth/callback"]);
```

`AddOidcServer(...)` registers persistent signing keys, revoked refresh tokens, clients, background cleanup/rotation, and an initialization step. The initialization step maps the OIDC/OAuth endpoints automatically and bootstraps configured clients after database migration. Supply a durable persistence provider in production.

Replace `IOAuthAuthorizeEndpoint` and `IOAuthSubjectService` with application implementations when using authorization-code/OIDC identity flows. Device, confidential, private-key JWT, and dynamic-registration surfaces are also available through the OIDC server package.

## JWT Resource Server

```csharp
builder.Services.AddAuthenticationResourceServer(options =>
{
    options.AuthorizationServerIssuer = "https://auth.example.com/oauth2";
    options.ExpectedIssuer = "https://auth.example.com/oauth2";
    options.Audience = "my-app";
    options.RequireHttpsMetadata = true;
});
```

The resource server discovers OAuth authorization-server metadata and JWKS from `AuthorizationServerIssuer`. It populates `IUserContext` and the tenant pipeline for RPC, command, and notification handling.

## Configuration Snippet

```json
{
  "ConnectionStrings": {
    "postgres": "Host=localhost;Database=myapp;Username=postgres;Password=postgres",
    "redis": "localhost:6379",
    "rabbitmq": "Host=localhost;Port=5672;UserName=guest;Password=guest;VirtualHost=/"
  },
  "NOF": {
    "OidcServer": {
      "SigningKeyEncryptionKey": "replace-with-secret-configuration"
    }
  }
}
```
