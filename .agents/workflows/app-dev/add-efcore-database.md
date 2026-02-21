---
description: How to set up EF Core with PostgreSQL, configure DbContext, and manage migrations in a NOF application
---

# Add EF Core Database with PostgreSQL

## 1. Add NuGet Packages

In the host project:
```bash
dotnet add package NOF.Infrastructure.EntityFrameworkCore.PostgreSQL
```

## 2. Create the DbContext

Create a class inheriting from `NOFDbContext` in the host project:

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Infrastructure.EntityFrameworkCore;

public class AppDbContext : NOFDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // IMPORTANT: always call base — it configures outbox/inbox/state machine tables
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CustomerName).HasMaxLength(200);

            // Value object properties are stored as their primitive type
            // The source-generated JsonConverter handles serialization automatically

            // Owned entities (child entities within the aggregate)
            entity.OwnsMany<OrderItem>("_items", item =>
            {
                item.ToTable("OrderItems");
                item.WithOwner().HasForeignKey("OrderId");
                item.HasKey("Id");
                item.Property("Id").ValueGeneratedOnAdd();
            });

            // Ignore navigation properties that are read-only projections
            entity.Ignore(e => e.Items);
        });
    }
}
```

## 3. Register in Program.cs

```csharp
builder.AddEFCore<AppDbContext>()
    .AutoMigrate()       // Auto-apply migrations on startup
    .UsePostgreSQL();    // Use PostgreSQL provider
```

## 4. Configure Connection String

In `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=myapp;Username=postgres;Password=postgres"
  }
}
```

## 5. Create and Apply Migrations

```bash
# Install EF Core tools (if not already)
dotnet tool install --global dotnet-ef

# Create a migration
dotnet ef migrations add InitialCreate --project MyApp --context AppDbContext

# Apply migrations manually (if not using AutoMigrate)
dotnet ef database update --project MyApp --context AppDbContext
```

## 6. Implement Repository with EF Core

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Annotation;
using NOF.Infrastructure.EntityFrameworkCore;

[AutoInject(Lifetime.Scoped)]
public class OrderRepository : EFCoreRepository<Order, OrderId>, IOrderRepository
{
    public OrderRepository(DbContext dbContext) : base(dbContext) { }

    public async Task<Order?> FindByCustomerAsync(string name, CancellationToken ct)
        => await DbSet.FirstOrDefaultAsync(o => o.CustomerName == name, ct);
}
```

## Notes

- `NOFDbContext` automatically configures outbox messages, inbox messages, and state machine context tables.
- **Value objects (`[ValueObject<T>]`) are automatically handled by EF Core** — `ValueObjectValueConverterSelector` detects them and provides `ValueConverter` instances at runtime. No manual converter registration needed.
- `AutoMigrate()` applies pending migrations on application startup — convenient for development, consider disabling in production.
- Domain events raised by aggregate roots are automatically dispatched when `IUnitOfWork.SaveChangesAsync()` is called.
- EF Core migrations are excluded from formatting rules (configured in `.editorconfig`).
- The `IUnitOfWork` is automatically registered as `EFCoreUnitOfWork` when you call `AddEFCore<T>()`.
