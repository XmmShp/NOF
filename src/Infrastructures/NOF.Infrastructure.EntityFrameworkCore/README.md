# NOF.Infrastructure.EntityFrameworkCore

Entity Framework Core persistence package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

`NOF.Infrastructure.EntityFrameworkCore` contains the EF Core-specific persistence implementation that used to live inside `NOF.Infrastructure`, including:

- `NOFDbContext`
- `UseDbContext<TDbContext>()`
- `EFCoreSelector`
- tenant-aware model customization and factory services
- EF-backed `IInboxMessageStore` and `IDbContext` adapter
- SQLite in-memory default persistence registration

## Installation

```shell
dotnet add package NOF.Infrastructure.EntityFrameworkCore
```

## Usage

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Hosting;
using NOF.Infrastructure.EntityFrameworkCore;

var builder = NOFWebApplicationBuilder.Create(args);

builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionString(builder.Configuration.GetConnectionString("postgres")
        ?? throw new InvalidOperationException("Connection string 'postgres' not found."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseNpgsql(connectionString))
    .MigrateOnInitialize();
```

For lightweight local or test scenarios, call `AddNOFEntityFrameworkCore()` to register the default SQLite in-memory persistence.

## Soft delete

Soft delete is enabled for all supported root entity types by default. Use `WithSoftDelete` to change the default for a `DbContext`, and `HasSoftDelete` in the EF model to override it for an individual root entity type:

```csharp
using Microsoft.EntityFrameworkCore.Metadata.Builders;

builder.UseDbContext<AppDbContext>()
    .WithSoftDelete(false);

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Order>(entity =>
    {
        entity.HasSoftDelete();
    });
}
```

The entity-level setting takes precedence over the `DbContext` default. Call `HasSoftDelete(false)` to opt a root entity type out when the default is enabled. Owned entity types inherit the lifecycle of their owner and cannot be configured independently.
