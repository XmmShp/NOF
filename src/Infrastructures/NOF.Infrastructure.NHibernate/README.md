# NOF.Infrastructure.NHibernate

NHibernate persistence package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

`NOF.Infrastructure.NHibernate` provides an NHibernate-based implementation of the NOF persistence abstractions.

Current scope:

- `UseNHibernate()`
- NHibernate-backed `IDbContext` and `IDbContextFactory`
- contributor-based entity mapping through `IDbContextModelCreatingContributor`
- tenant-aware connection-string resolution for `TenantMode.DatabasePerTenant`

Current limitations:

- `TenantMode.SharedDatabase` is not supported yet
- `AsNoTracking()` currently returns the same LINQ query shape without a dedicated no-tracking session
- schema management is limited to NHibernate `SchemaUpdate`

## Usage

```csharp
using NHibernate.Dialect;
using NHibernate.Driver;
using NOF.Infrastructure.NHibernate;

var builder = NOFWebApplicationBuilder.Create(args);

builder.UseNHibernate()
    .WithConnectionString("Data Source=nof-{tenantId}.db")
    .WithOptions((configuration, connectionString) =>
    {
        configuration.DataBaseIntegration(db =>
        {
            db.ConnectionString = connectionString;
            db.Dialect<SQLiteDialect>();
            db.Driver<SQLite20Driver>();
        });
    })
    .BuildSchemaOnInitialize();
```
