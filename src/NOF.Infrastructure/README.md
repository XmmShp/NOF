# NOF.Infrastructure

Unified infrastructure entry package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

`NOF.Infrastructure` provides the default runtime wiring for NOF applications, including:

- builder defaults and step orchestration
- in-memory cache and messaging riders
- OpenTelemetry registration and transport middleware
- JWT resource server validation and command/notification token propagation
- ambient `IMapper` and `IIdGenerator` activation through scoped `IDaemonService` implementations

This lets consumers reference one package/project while still getting the full default infrastructure setup.

## Usage

Add a single reference to `NOF.Infrastructure` in host or infrastructure-adapter projects.

## Installation

```shell
dotnet add package NOF.Infrastructure
```

## Built-in Capabilities

This package includes:

- in-memory cache (`ICacheService` + `MemoryCacheServiceRider`)
- local file system object storage (`IObjectStorage` + `FileSystemObjectStorageRider`), with an optional `MemoryObjectStorageRider` for tests
- in-memory backplane (`IBackplane` + `MemoryBackplane`)
- in-memory riders (`MemoryCommandRider`, `MemoryNotificationRider`)
- in-memory persistence for tests/development (`services.AddInMemoryPersistence()`)
- in-process event publisher (`IEventPublisher`)
- database-agnostic persistence adapters for `IDbContext`, Domain `IRepository<T>`, and async query extensions
- outbox / inbox entities and transactional message background services
- JWT resource server primitives (`services.AddAuthenticationResourceServer(...)`, JWKS fetching/cache, inbound token validation)

The default in-memory cache implementation is isolated per NOF host:

- cache data lives in `MemoryCacheServiceRiderState`
- local `GetOrSetAsync(...)` locks live in `CacheServiceLocalLockState`
- both are registered as DI singletons instead of process-wide `static` state

The default backplane implementation is also host-local:

- subscriptions live in `MemoryBackplaneState`
- published messages are delivered only to subscribers inside the same NOF host process

The default object storage implementation persists content and metadata locally under `AppContext.BaseDirectory/App_Data/objects`. Configure a writable, persistent directory through `builder.Services.AddFileSystemObjectStorage(options => options.RootPath = "/data/objects")`. Relative paths resolve against the working directory. The directory is provider-owned: object names are hashed, and files contain both metadata and content; do not modify its files or add symbolic links. Writes replace complete objects atomically, and reads retain a snapshot across replacement or deletion. Listing uses ordinal key order and prefix matching; it scans the bucket's metadata files.

For isolated tests, use `builder.Services.AddMemoryObjectStorage()`. `NOFTestAppBuilder` selects this automatically; state is shared between scopes within one host and discarded when that host is disposed. Replace `IObjectStorageRider` in a provider package to connect another backend while keeping application code unchanged. For AWS S3 and S3-compatible services, reference `NOF.Infrastructure.AmazonS3`:

```csharp
builder.Services.AddAmazonS3ObjectStorage(options =>
{
    options.Region = "ap-southeast-1";
});
```

Object keys can be isolated by tenant through `ObjectStorageOptions.KeyPrefix`:

```csharp
builder.Services.Configure<ObjectStorageOptions>(options =>
    options.KeyPrefix = "tenants/{tenantId}/");
```

`IObjectStorage.IgnoreKeyPrefix()` provides an explicit administrative view over physical keys when cross-tenant access is required.

## Local RPC Clients

The Infrastructure source generator discovers `RpcServer<TService>` implementations in the host and referenced application assemblies. For a server named `OrderService`, it generates a public `LocalOrderServiceClient` implementing the canonical client interface generated beside the service contract:

```csharp
public partial class OrderService : RpcServer<IOrderService>;

builder.Services.ReplaceOrAddScoped<IOrderServiceClient, LocalOrderServiceClient>();
```

No `LocalRpcClientAttribute` or user-authored empty partial client class is required. Both the Contract and Infrastructure generators derive `IOrderServiceClient` independently from `IOrderService`; the Infrastructure generator never reads Contract generator output.

Local calls preserve the same request boundary as a remote RPC for dependency-injection lifetimes. The outbound pipeline runs in the caller's scope. Its terminal dispatch creates and asynchronously disposes a new scope for RPC server resolution, daemon-service activation, the inbound pipeline, and the handler. Scoped dependencies such as `DbContext` are therefore not shared between the caller and handler, while headers produced by outbound middleware cross the boundary.

Contracts marked with `[TransportOverMemory]` use the same local client path. Their server registrations remain available to `RpcServerInvoker`. The transport initialization step stays transport-agnostic and passes every registration to every `IRpcServerTransport`; each transport inspects the contract metadata and decides whether it applies. The built-in HTTP transport ignores memory-only contracts.

## RPC Server Transport Extension Point

`NOF.Infrastructure` owns the framework-level RPC server transport initialization step. Transport packages implement `IRpcServerTransport` and register the implementation in DI:

```csharp
public sealed class MyRpcServerTransport : IRpcServerTransport
{
    public TopologyComparison Compare(IRpcServerTransport other)
        => TopologyComparison.DoesNotMatter;

    public Task MapAsync(IHost host, RpcServerRegistration registration)
    {
        // Inspect the contract metadata and map this registration.
        return Task.CompletedTask;
    }
}

services.AddRpcServerTransport<MyRpcServerTransport>();
```

At application initialization, NOF orders all registered transports through the normal topology mechanism and passes every `RpcServerRegistration`, together with the built `IHost`, to each transport. The transport implementation owns applicability checks and mapping behavior.

## Persistence Providers

`NOF.Infrastructure` no longer ships a built-in EF Core implementation. Database persistence is provided by adapter packages such as `NOF.Infrastructure.EntityFrameworkCore`.

After adding the EF Core package, persistence is configured through `UseDbContext<TDbContext>()` and `EFCoreSelector`:

```csharp
using Microsoft.EntityFrameworkCore;
using NOF.Infrastructure.EntityFrameworkCore;

var builder = NOFWebApplicationBuilder.Create(args);

builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionString(builder.Configuration.GetConnectionString("postgres")
        ?? throw new InvalidOperationException("Connection string 'postgres' not found."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseNpgsql(connectionString))
    .MigrateOnInitialize();
```

Available configuration methods:

- `WithTenantMode(...)`
- `WithConnectionString(...)`
- `WithConnectionStringResolver(...)`
- `WithOptions(...)`
- `MigrateOnInitialize()`

The resolver receives the normalized tenant identifier, concrete `DbContext` type, tenant mode, fallback template, and scoped services. Use it for tenant catalogs, secret stores, or shard maps; the template remains the convenient static default.

`MigrateOnInitialize()` migrates the context resolved during host initialization; it cannot
discover application-owned tenant databases. A custom deployment migrator can resolve
`ITenantDbContextFactory<TDbContext>` from a scope and call
`MigrateAsync(tenantId, cancellationToken)` for each tenant.

Transactional inbox and outbox processors also need the application's tenant catalog to
poll database-per-tenant message tables. Register an `ITransactionalMessageTenantProvider`
that enumerates tenant IDs from durable storage:

```csharp
builder.Services.AddSingleton<ITransactionalMessageTenantProvider, AppTenantCatalog>();
```

`AppTenantCatalog.GetTenantIdsAsync` should return every active tenant database, including
tenants with messages written before this process started. NOF always polls the host database
and deduplicates normalized tenant IDs. The default provider returns no additional tenants.
The same catalog is used by inbox and outbox cleanup. A catalog failure is logged and retried
at the next polling interval; an error in one tenant database does not prevent other tenants
from being processed.

## SQLite

For SQLite, provide the provider configuration via `WithOptions(...)`:

```csharp
builder.UseDbContext<AppDbContext>()
    .WithTenantMode(TenantMode.SharedDatabase)
    .WithConnectionString(builder.Configuration.GetConnectionString("sqlite")
        ?? throw new InvalidOperationException("Connection string 'sqlite' not found."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseSqlite(connectionString))
    .MigrateOnInitialize();
```

## In-Memory SQLite

For tests or lightweight local scenarios, `NOF.Infrastructure.EntityFrameworkCore` provides `AddNOFEntityFrameworkCore()` to register the default SQLite in-memory persistence.

## License

Apache-2.0
