---
description: Configure the NOF Entity Framework Core provider with PostgreSQL, migrations, tenancy, and soft delete
---

# Add EF Core Database with PostgreSQL

## 1. Add Packages to the Host

```bash
dotnet add package NOF.Infrastructure.EntityFrameworkCore
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

`NOF.Infrastructure.EntityFrameworkCore` is the package that contains `NOFDbContext` and `UseDbContext<T>()`; those APIs are not in the core `NOF.Infrastructure` package.

## 2. Create the Host DbContext

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Infrastructure.EntityFrameworkCore;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : NOFDbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(order => order.Id);
        });
    }
}
```

Always call `base.OnModelCreating(modelBuilder)` so registered NOF model contributors are applied.

String-backed value objects should declare `[ValueObjectLength]` in domain code. The provider uses that metadata for maximum length; avoid duplicating `HasMaxLength(...)`.

## 3. Register the Provider

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Hosting;
using NOF.Infrastructure.EntityFrameworkCore;

builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionString(builder.Configuration.GetConnectionString("postgres")
        ?? throw new InvalidOperationException("Connection string 'postgres' not found."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseNpgsql(connectionString))
    .WithSoftDelete(true)
    .MigrateOnInitialize();
```

For database-per-tenant mode, the connection string may contain `{tenantId}`. Shared-database mode adds tenant shadow state and filters. Soft delete is enabled by default and can be overridden per root entity with `HasSoftDelete(...)`.

## 4. Configure the Connection String

```json
{
  "ConnectionStrings": {
    "postgres": "Host=localhost;Database=myapp;Username=postgres;Password=postgres"
  }
}
```

## 5. Create and Apply Migrations

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project MyApp --startup-project MyApp --context AppDbContext
dotnet ef database update --project MyApp --startup-project MyApp --context AppDbContext
```

`MigrateOnInitialize()` adds a host initialization step that applies migrations before later components such as OIDC client bootstrapping.

## Application-Layer Usage

Application handlers inject `IDbContext` or `IRepository<T>`, not EF Core `DbContext`:

```csharp
public sealed class CreateOrder(IDbContext dbContext) : OrderService.CreateOrder
{
    public override async Task<Result> HandleAsync(
        CreateOrderRequest request,
        Context context,
        CancellationToken cancellationToken)
    {
        var order = Order.Create(EmailAddress.Of(request.CustomerEmail));
        dbContext.Set<Order>().Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```
