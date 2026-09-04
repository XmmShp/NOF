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

`MigrateOnInitialize()` migrates the context resolved during host initialization. It does not
enumerate application-defined tenant databases in `DatabasePerTenant` mode; use the explicit
tenant migration API below when those databases must be migrated as part of deployment.

For lightweight local or test scenarios, call `AddNOFEntityFrameworkCore()` to register the default SQLite in-memory persistence.

## Dynamic connection resolution

`WithConnectionString(...)` remains the simple default and supports the `{tenantId}` placeholder. Use `WithConnectionStringResolver(...)` when the connection must come from a tenant catalog, secret store, shard map, or another scoped service:

```csharp
builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionStringResolver(context =>
    {
        var catalog = context.Services.GetRequiredService<ITenantDatabaseCatalog>();
        return catalog.GetConnectionString(context.DbContextType, context.TenantId);
    })
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseNpgsql(connectionString))
    .MigrateOnInitialize();
```

The resolution context exposes the normalized tenant identifier, concrete `DbContext` type, tenant mode, fallback template, and the current scoped service provider. Resolution is synchronous because NOF's `IDbContextFactory` creates contexts synchronously; cache remotely acquired secrets or connection metadata in the resolver's dependency.

## Custom tenant migration orchestration

`UseDbContext<TDbContext>()` registers `ITenantDbContextFactory<TDbContext>`. It creates a strongly
typed context for an explicit tenant without changing `ICurrentTenant`, and reuses the configured
tenant connection-string resolver and provider options. The `MigrateAsync` extension makes it
convenient to build a deployment job around an application-owned tenant catalog:

```csharp
public sealed class TenantDatabaseMigrator(
    IServiceScopeFactory scopeFactory,
    ITenantCatalog tenantCatalog)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var tenant in tenantCatalog.GetAllAsync(cancellationToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var factory = scope.ServiceProvider
                .GetRequiredService<ITenantDbContextFactory<AppDbContext>>();

            await factory.MigrateAsync(tenant.Id, cancellationToken);
        }
    }
}
```

The application owns tenant discovery, retries, concurrency limits, and deployment coordination.
The factory owns tenant normalization and connection resolution; the migration helper owns context
disposal and delegates locking and migration execution to EF Core.

### Custom database context factory

`NOFDbContextFactory<TDbContext>` is public and its typed context creation methods are virtual.
Register a derived factory with the two-type-parameter `UseDbContext` overload when context creation
requires behavior beyond connection-string resolution:

```csharp
public sealed class AppDbContextFactory(
    IServiceProvider services,
    ITenantDatabaseProvisioner provisioner)
    : NOFDbContextFactory<AppDbContext>(services)
{
    public override AppDbContext CreateDbContext(string tenantId)
    {
        provisioner.EnsureProvisioned(tenantId);
        return base.CreateDbContext(tenantId);
    }
}

builder.UseDbContext<AppDbContext, AppDbContextFactory>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionStringResolver(/* ... */)
    .WithOptions(/* ... */);
```

The custom factory is registered as itself and as `NOFDbContextFactory<TDbContext>`,
`ITenantDbContextFactory<TDbContext>`, EF Core's `IDbContextFactory<TDbContext>`, and NOF's
provider-neutral `IDbContextFactory`. Prefer `WithConnectionStringResolver(...)` when only database
routing differs; derive the factory when the context creation lifecycle itself must change.

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
