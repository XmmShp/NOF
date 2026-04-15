# Repository & Unit of Work

## Overview

NOF provides an explicit persistence model built on DDD principles. Repositories are collection-like abstractions for aggregate roots, while the Unit of Work coordinates transactional persistence with explicit change tracking.

**Design philosophy: explicit > implicit.** All mutations must be explicitly declared via `IUnitOfWork.Update()` before `SaveChangesAsync()`. EF Core's automatic change detection is disabled.

## Domain Layer

### IEntity

Marker interface for child entities owned by an aggregate root. No base class.

```csharp
public interface IEntity;
```

### IAggregateRoot

```csharp
public interface IAggregateRoot : IEntity
{
    ICollection<IEvent> Events { get; }
}
```

- `Events` is `ICollection<IEvent>` — users can freely add, remove, or clear events.
- Domain events are dispatched during `SaveChangesAsync()`, then cleared.

### AggregateRoot (base class)

```csharp
public abstract class AggregateRoot : IAggregateRoot
{
    public virtual ICollection<IEvent> Events { get; } = [];

    protected virtual void AddEvent(IEvent @event)
    {
        Events.Add(@event);
    }
}
```

- No `Entity` base class — `AggregateRoot` directly implements `IAggregateRoot`.
- `AddEvent()` is a convenience method; users can also manipulate `Events` directly.

### IRepository\<T\>

```csharp
public interface IRepository<TAggregateRoot> : IRepository
    where TAggregateRoot : class, IAggregateRoot
{
    ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken ct = default);
    IAsyncEnumerable<TAggregateRoot> FindAllAsync(CancellationToken ct = default);
    void Add(TAggregateRoot entity);
    void Remove(TAggregateRoot entity);
}
```

Typed-key variants (`IRepository<T, TKey>`, `IRepository<T, TKey1, TKey2>`, etc.) provide convenience `FindAsync` overloads that delegate to the `object?[]` version.

**Design decisions:**

- **`FindAllAsync` returns `IAsyncEnumerable<T>`** — supports streaming large result sets without loading everything into memory.
- **No `Update` method** — Repository is a collection abstraction (Find/Add/Remove). Update is a persistence concern handled by `IUnitOfWork`.
- **Custom queries** — defined on domain-specific repository interfaces (e.g., `IOrderRepository.FindByCustomerAsync`).

## Application Layer

### IUnitOfWork

```csharp
public interface IUnitOfWork
{
    void Update<TAggregateRoot>(TAggregateRoot entity)
        where TAggregateRoot : class, IAggregateRoot;

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

**`Update(entity)`** — marks an aggregate root and its entire object graph as modified. Must be called explicitly after any mutation to an existing aggregate root.

**`SaveChangesAsync()`** — in a single transaction:
1. Collects domain events from all tracked `IAggregateRoot` entities
2. Dispatches events to `InMemoryEventHandler<T>` handlers
3. Writes outbox messages (if any deferred notifications/commands were queued)
4. Persists all changes to the database
5. Commits the transaction

### When to call Update

| Operation | Repository/UoW call |
|-----------|-------------------|
| Create new aggregate | `_repo.Add(entity)` |
| Modify existing aggregate | `_uow.Update(entity)` |
| Delete aggregate | `_repo.Remove(entity)` |
| Modify child entities (owned) | `_uow.Update(aggregateRoot)` |

**Key rule:** `Add` and `Remove` handle their own tracking. Only mutations to *existing* entities require `Update`.

## EF Core Implementation

### NOFDbContext

```csharp
protected NOFDbContext(DbContextOptions options) : base(options)
{
    ChangeTracker.AutoDetectChangesEnabled = false;
}
```

`AutoDetectChangesEnabled = false` ensures no implicit change detection. All mutations flow through explicit `Update()` calls.

### EFCoreUnitOfWork

```csharp
public void Update<TAggregateRoot>(TAggregateRoot entity)
    where TAggregateRoot : class, IAggregateRoot
{
    _dbContext.Update(entity).DetectChanges();
}
```

**Why `.DetectChanges()` after `.Update()`?**

- `DbContext.Update(entity)` marks the root entity as `Modified`, but does **not** traverse navigation properties to discover new (untracked) child entities.
- `EntityEntry.DetectChanges()` performs a **local graph traversal** from the entity, discovering:
  - New child entities added to collections → marked as `Added`
  - Existing tracked child entities → marked as `Modified`
  - Removed child entities → handled by EF Core's orphan deletion

This is distinct from the global `ChangeTracker.DetectChanges()` — it only examines the specified entity's object graph, not all tracked entities.

### EFCoreRepository\<T\>

```csharp
public virtual ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken ct)
    => DbContext.Set<TAggregateRoot>().FindAsync(keyValues, ct);

public virtual IAsyncEnumerable<TAggregateRoot> FindAllAsync(CancellationToken ct = default)
    => DbContext.Set<TAggregateRoot>().AsAsyncEnumerable();

public virtual void Add(TAggregateRoot entity)
    => DbContext.Set<TAggregateRoot>().Add(entity);

public virtual void Remove(TAggregateRoot entity)
    => DbContext.Set<TAggregateRoot>().Remove(entity);
```

## Usage Example

```csharp
// Create
var order = Order.Create("Alice");
_orderRepo.Add(order);
await _uow.SaveChangesAsync(ct);

// Update (aggregate root mutation)
var order = await _orderRepo.FindAsync(orderId, ct);
order.UpdateName("Bob");
_uow.Update(order);
await _uow.SaveChangesAsync(ct);

// Update (child entity mutation)
var order = await _orderRepo.FindAsync(orderId, ct);
order.AddItem(new OrderItem("Widget", 3));  // Modifies child collection
_uow.Update(order);  // DetectChanges() finds the new OrderItem
await _uow.SaveChangesAsync(ct);

// Delete
var order = await _orderRepo.FindAsync(orderId, ct);
order.MarkAsDeleted();  // Raise domain event if needed
_orderRepo.Remove(order);
await _uow.SaveChangesAsync(ct);

// Query all
await foreach (var order in _orderRepo.FindAllAsync(ct))
{
    // Process each order in a streaming fashion
}
```

## Domain Services

When a domain service modifies aggregate roots, the application layer is responsible for calling `Update()`. Since the application layer holds references to the aggregates it passes into domain services, it knows exactly which entities were modified:

```csharp
// Application handler
var from = await _accountRepo.FindAsync(fromId, ct);
var to = await _accountRepo.FindAsync(toId, ct);
_transferService.Transfer(from, to, amount);  // Domain service modifies both
_uow.Update(from);
_uow.Update(to);
await _uow.SaveChangesAsync(ct);
```

## Design Rationale

### Why explicit Update instead of auto-detect?

1. **Infrastructure independence** — The domain interface (`IUnitOfWork.Update`) doesn't assume EF Core's change tracker. Non-EF implementations (Dapper, MongoDB, etc.) can implement `Update` without change tracking.
2. **Performance** — Global `DetectChanges()` scans all tracked entities on every `SaveChanges`, `Add`, query, etc. With `AutoDetectChangesEnabled = false`, only the entities explicitly passed to `Update()` are examined.
3. **Clarity** — The code explicitly declares intent: "I modified this entity, persist it." No hidden magic.

### Why Update on IUnitOfWork, not IRepository?

- **Repository = collection abstraction** (Find/Add/Remove ≈ query/insert/delete from a set)
- **UnitOfWork = persistence coordinator** (Update + SaveChanges = "prepare and commit")
- Update is a cross-cutting persistence concern, not a collection operation.

### Why IAsyncEnumerable for FindAllAsync?

- Supports streaming — doesn't load all entities into memory at once
- Composable with `await foreach`, LINQ operators
- Consistent with EF Core's `AsAsyncEnumerable()`

### Why ICollection\<IEvent\> for Events?

- Gives users full control — add, remove, clear events as needed
- `AggregateRoot.AddEvent()` is a convenience, not the only way
- Respects the principle: "don't understand, but respect" user intent
