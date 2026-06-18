using NOF.Application;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

[RequiresDynamicCode("The in-memory persistence provider exposes LINQ IQueryable over in-memory collections and is intended for tests/development, not Native AOT.")]
[RequiresUnreferencedCode("The in-memory persistence provider snapshots arbitrary entity types via reflection and is intended for tests/development, not trimmed applications.")]
internal sealed class InMemoryDbContext(InMemoryPersistenceStore store) : IDbContext
{
    private readonly List<InMemoryPersistenceChange> _changes = [];
    private readonly Dictionary<Type, List<TrackedInMemoryEntity>> _trackedEntities = [];

    public IDbSet<TEntity> Set<TEntity>()
        where TEntity : class
        => new InMemoryDbSet<TEntity>(this, store);

    public int SaveChanges()
        => SaveChanges(acceptAllChangesOnSuccess: true);

    public int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var changes = BuildChanges();
        var count = store.Save(changes);
        if (acceptAllChangesOnSuccess)
        {
            _changes.Clear();
            AcceptTrackedChanges();
        }

        return count;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SaveChanges(acceptAllChangesOnSuccess));
    }

    public IDbContextTransaction BeginTransaction()
        => new InMemoryDbContextTransaction(store);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BeginTransaction());
    }

    internal IQueryable<TEntity> Query<TEntity>(bool track = true)
        where TEntity : class
    {
        var entities = store.Snapshot<TEntity>();
        if (track)
        {
            foreach (var entity in entities)
            {
                Track(entity);
            }
        }

        foreach (var change in _changes.Where(change => change.EntityType == typeof(TEntity)))
        {
            var entity = (TEntity)change.Entity;
            switch (change.Kind)
            {
                case InMemoryPersistenceChangeKind.Add:
                    entities.Add(entity);
                    break;
                case InMemoryPersistenceChangeKind.Update:
                    ReplaceOrAdd(entities, entity);
                    break;
                case InMemoryPersistenceChangeKind.Remove:
                    entities.RemoveAll(item => ReferenceEquals(item, entity) || InMemoryPersistenceStore.SameKey(item, entity));
                    break;
            }
        }

        return entities.AsQueryable();
    }

    internal void AddChange(InMemoryPersistenceChangeKind kind, Type entityType, object entity)
        => _changes.Add(new InMemoryPersistenceChange(kind, entityType, entity));

    private IReadOnlyCollection<InMemoryPersistenceChange> BuildChanges()
    {
        var changes = new List<InMemoryPersistenceChange>(_changes);
        foreach (var pair in _trackedEntities)
        {
            foreach (var tracked in pair.Value)
            {
                if (IsAlreadyExplicitlyChanged(changes, tracked.Entity))
                {
                    continue;
                }

                if (InMemoryPersistenceStore.SameValues(tracked.Entity, tracked.Original))
                {
                    continue;
                }

                changes.Add(new InMemoryPersistenceChange(
                    InMemoryPersistenceChangeKind.Update,
                    pair.Key,
                    tracked.Entity));
            }
        }

        return changes;
    }

    private static bool IsAlreadyExplicitlyChanged(IEnumerable<InMemoryPersistenceChange> changes, object entity)
        => changes.Any(change => ReferenceEquals(change.Entity, entity) || InMemoryPersistenceStore.SameKey(change.Entity, entity));

    private void Track<TEntity>(TEntity entity)
        where TEntity : class
    {
        var entityType = typeof(TEntity);
        if (!_trackedEntities.TryGetValue(entityType, out var tracked))
        {
            tracked = [];
            _trackedEntities[entityType] = tracked;
        }

        if (tracked.Any(item => ReferenceEquals(item.Entity, entity) || InMemoryPersistenceStore.SameKey(item.Entity, entity)))
        {
            return;
        }

        tracked.Add(new TrackedInMemoryEntity(entity, InMemoryPersistenceStore.CloneEntity(entity)));
    }

    private void AcceptTrackedChanges()
    {
        foreach (var tracked in _trackedEntities.Values.SelectMany(static entities => entities))
        {
            tracked.AcceptChanges();
        }
    }

    private static void ReplaceOrAdd<TEntity>(List<TEntity> entities, TEntity entity)
        where TEntity : class
    {
        var index = entities.FindIndex(item => ReferenceEquals(item, entity) || InMemoryPersistenceStore.SameKey(item, entity));
        if (index < 0)
        {
            entities.Add(entity);
            return;
        }

        entities[index] = entity;
    }
}

[RequiresDynamicCode("The in-memory persistence provider snapshots arbitrary entity types and is intended for tests/development, not Native AOT.")]
[RequiresUnreferencedCode("The in-memory persistence provider snapshots arbitrary entity types via reflection and is intended for tests/development, not trimmed applications.")]
internal sealed class TrackedInMemoryEntity(object entity, object original)
{
    public object Entity { get; } = entity;
    public object Original { get; private set; } = original;

    public void AcceptChanges()
    {
        Original = InMemoryPersistenceStore.CloneEntity(Entity);
    }
}

internal sealed class InMemoryDbSet<TEntity> : AsyncQueryable<TEntity>, IDbSet<TEntity>
    where TEntity : class
{
    private readonly InMemoryDbContext _dbContext;

    public InMemoryDbSet(InMemoryDbContext dbContext, InMemoryPersistenceStore store)
        : base(dbContext.Query<TEntity>(), new InMemoryPersistenceAsyncQueryExecutor<TEntity>(store))
    {
        _dbContext = dbContext;
    }

    public void Add(TEntity entity)
        => _dbContext.AddChange(InMemoryPersistenceChangeKind.Add, typeof(TEntity), entity);

    public ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Add(entity);
        return ValueTask.CompletedTask;
    }

    public void AddRange(IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
        {
            Add(entity);
        }
    }

    public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AddRange(entities);
        return Task.CompletedTask;
    }

    public void Attach(TEntity entity)
    {
        _ = entity;
    }

    public void AttachRange(IEnumerable<TEntity> entities)
    {
        _ = entities;
    }

    public void Update(TEntity entity)
        => _dbContext.AddChange(InMemoryPersistenceChangeKind.Update, typeof(TEntity), entity);

    public void UpdateRange(IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
        {
            Update(entity);
        }
    }

    public void Remove(TEntity entity)
        => _dbContext.AddChange(InMemoryPersistenceChangeKind.Remove, typeof(TEntity), entity);

    public void RemoveRange(IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
        {
            Remove(entity);
        }
    }

    public IAsyncQueryable<TEntity> AsNoTracking()
        => new AsyncQueryable<TEntity>(_dbContext.Query<TEntity>(track: false), AsyncExecutor);
}

internal sealed class InMemoryPersistenceAsyncQueryExecutor<TEntity>(InMemoryPersistenceStore store) : InMemoryAsyncQueryExecutor
    where TEntity : class
{
    public override Task<int> ExecuteDeleteAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return typeof(TSource) == typeof(TEntity)
            ? Task.FromResult(store.ExecuteDelete(source.Cast<TEntity>()))
            : base.ExecuteDeleteAsync(source, cancellationToken);
    }

    public override Task<int> ExecuteUpdateAsync<TSource>(IQueryable<TSource> source, IUpdateSetters<TSource> setters, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(setters);
        return typeof(TSource) == typeof(TEntity)
            ? Task.FromResult(store.ExecuteUpdate(source.Cast<TEntity>(), (IUpdateSetters<TEntity>)setters))
            : base.ExecuteUpdateAsync(source, setters, cancellationToken);
    }
}

internal sealed class InMemoryDbContextTransaction : IDbContextTransaction
{
    private readonly InMemoryPersistenceStore _store;
    private readonly InMemoryPersistenceSnapshot _snapshot;
    private readonly Dictionary<string, InMemoryPersistenceSnapshot> _savepoints = new(StringComparer.Ordinal);
    private bool _completed;

    public InMemoryDbContextTransaction(InMemoryPersistenceStore store)
    {
        _store = store;
        _snapshot = store.CaptureSnapshot();
    }

    public Guid TransactionId { get; } = Guid.NewGuid();

    public void Commit()
        => _completed = true;

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Commit();
        return Task.CompletedTask;
    }

    public void Rollback()
    {
        _store.RestoreSnapshot(_snapshot);
        _completed = true;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Rollback();
        return Task.CompletedTask;
    }

    public void CreateSavepoint(string name)
        => _savepoints[name] = _store.CaptureSnapshot();

    public Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CreateSavepoint(name);
        return Task.CompletedTask;
    }

    public void RollbackToSavepoint(string name)
    {
        if (!_savepoints.TryGetValue(name, out var snapshot))
        {
            throw new DbTransactionException($"Savepoint '{name}' does not exist.");
        }

        _store.RestoreSnapshot(snapshot);
    }

    public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RollbackToSavepoint(name);
        return Task.CompletedTask;
    }

    public void ReleaseSavepoint(string name)
        => _savepoints.Remove(name);

    public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReleaseSavepoint(name);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_completed)
        {
            Rollback();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
