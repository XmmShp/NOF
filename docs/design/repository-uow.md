# Repository & Unit of Work

## Recommended persistence boundary

Application handlers depend on `NOF.Application.IDbContext` and
`NOF.Domain.IRepository<T>` returned by `dbContext.Set<T>()`. Async LINQ helpers live
in `NOF.Application`. `IDbContext` exposes saving and explicit transactions through
`IDbContextTransaction`; this is the application's unit of work boundary.

The Host selects EF Core or NHibernate. For EF Core, the Host owns `NOFDbContext`,
model configuration, migrations, provider options, and `UseDbContext<AppDbContext>()`.
Concrete ORM dependencies belong in Host infrastructure implementations of
application-owned interfaces, including native SQL and locking operations.

See the existing [sample handler](../../sample/NOF.Sample.Application/RequestHandlers/GetConfigNodeChildren.cs)
for provider-neutral queries and the [EF Core setup](../../src/Infrastructures/NOF.Infrastructure.EntityFrameworkCore/README.md)
for Host configuration. These sources participate in the sample build.

## Transactional messages

Await `DeferPublishAsync(message, context, cancellationToken)` or
`DeferSendAsync(message, context, cancellationToken)`, then save the **same** scoped
`IDbContext` that tracks the business changes. Deferred dispatch stages outbox rows;
it does not immediately deliver messages. Pass the current handler's explicit
`Context`, subject to the [tenant trust rules](../../src/NOF.Infrastructure/README.md#tenant-authorization).

The following source is compiled as part of `NOF.Sample.Application`; keep the
example there so API changes break the sample build:

[!code-csharp[](../../sample/NOF.Sample.Application/Integration/TransactionalMessages.cs)]

Without an explicit transaction, the relational provider's save transaction commits
the staged business and outbox rows together. With an explicit transaction,
`SaveChangesAsync()` flushes pending changes inside it; only `CommitAsync()` commits
the transaction. Multiple saves do not commit the surrounding transaction.
Rolling back after saving removes the database writes but does not undo CLR objects,
accepted change-tracker state, or arbitrary external side effects.

After a save failure, rollback, or commit whose outcome is unknown, dispose the scope
and recover with a fresh context. Use a stable application operation ID with a unique
database constraint to reconcile an uncertain commit before replaying it. A commit
exception does not establish that the server rolled back. Recreate the entire unit
of work for a retry; NOF does not supply application-level exactly-once execution.
Consumers must be idempotent because delivery and acknowledgement can be repeated.

## Native operations in the Host

`UseDbContext<TDbContext>()` registers one scoped concrete context, shared by
`DbContext`, `NOFDbContext`, `TDbContext`, and the `IDbContext` adapter. Inject
`AppDbContext` into a Host implementation of an application interface. Run EF
`FromSqlInterpolated`/`ExecuteSqlInterpolatedAsync` on that context, or use its
`Database.GetDbConnection()` and `Database.CurrentTransaction.GetDbTransaction()`
for a command. Open a transaction through the application `IDbContext` before
acquiring locks, and keep it alive until all business writes and outbox rows commit.
Parameterize values; identifiers must come from trusted application code.

Factory calls intentionally create independent contexts. Do not create another
context through `IDbContextFactory`/`ITenantDbContextFactory<T>` for native operations
that must join this transaction. Do not resolve persistence before the effective
tenant has been authorized and bound, or switch tenant within an existing scope.

PostgreSQL `FOR UPDATE SKIP LOCKED` remains a Host concern. The SQLite regression
`ScopedPersistence_ShouldShareTrackingAndTransaction` covers shared tracking and
commit/rollback over multiple saves for default and custom contexts. PostgreSQL
locking and transactional delivery must be verified separately against that provider.
