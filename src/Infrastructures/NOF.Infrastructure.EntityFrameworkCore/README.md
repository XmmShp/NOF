# NOF.Infrastructure.EntityFrameworkCore

Entity Framework Core infrastructure package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the EF Core integration layer: `NOFDbContext` base class, repository implementations, transactional outbox/inbox pattern, multi-tenancy support with automatic model filtering, and database context factory.

## Features

### NOFDbContext

Base `DbContext` with built-in support for:

- **Outbox/Inbox Messages** — reliable transactional messaging
- **State Machine Contexts** — persistent state machine storage
- **Tenant Management** — `EFCoreTenant` entity for multi-tenant scenarios

### Multi-Tenancy

Automatic tenant isolation at the database level:

- **`[HostOnly]`** entities are excluded from tenant database models via `HostOnlyModelFinalizingConvention`
- **Migration filtering** — `NOFTenantMigrationsSqlGenerator` skips host-only table operations in tenant databases
- **Custom ignored types** — override `GetTenantIgnoredEntityTypes()` for additional tenant-mode exclusions

```csharp
public class AppDbContext : NOFDbContext
{
    protected override Type[] GetTenantIgnoredEntityTypes() =>
        [typeof(GlobalConfigEntity), typeof(AuditLogEntity)];
}
```

### Transactional Outbox

Messages sent within handlers are persisted to the outbox table and delivered reliably via background services. Includes automatic cleanup for processed messages.

### Repository Pattern

Generic `IRepository<TAggregateRoot>` implementation backed by EF Core with automatic domain event dispatching.

## Installation

```shell
dotnet add package NOF.Infrastructure.EntityFrameworkCore
```

## License

Apache-2.0
