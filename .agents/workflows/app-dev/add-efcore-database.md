---
description: How to set up EF Core with PostgreSQL, configure DbContext, and manage migrations in a NOF application
---

# Add EF Core Database with PostgreSQL

## 1. Add NuGet Packages

In the host project:

```bash
dotnet add package NOF.Infrastructure
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

## 2. Create the DbContext

Create a class inheriting from `NOFDbContext` in the host project:

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Infrastructure;

public class AppDbContext : NOFDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CustomerEmail).HasMaxLength(200);
        });
    }
}
```

## 3. Register in Program.cs

```csharp
builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionString(builder.Configuration.GetConnectionString("postgres")
        ?? throw new InvalidOperationException("Connection string 'postgres' not found."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseNpgsql(connectionString))
    .MigrateOnInitialize();
```

## 4. Configure Connection String

In `appsettings.json`:

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
dotnet ef migrations add InitialCreate --project MyApp --context AppDbContext
dotnet ef database update --project MyApp --context AppDbContext
```

## Notes

- `NOFDbContext` configures outbox, inbox, and state machine context tables.
- Value objects implementing `IValueObject<T>` are handled automatically.
- `MigrateOnInitialize()` applies pending migrations on startup through an initialization step.
- Application handlers persist entities directly through `DbContext` / `NOFDbContext`.
