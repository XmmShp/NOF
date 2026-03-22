using NOF.Domain;

namespace NOF.Infrastructure.Memory;

public abstract class MemoryRepository<TAggregateRoot> : IRepository<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot, ICloneable
{
    private readonly Func<TAggregateRoot, object?[], bool> _selector;

    protected MemoryRepository(
        MemoryPersistenceContext context,
        Func<TAggregateRoot, object?[], bool> selector)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selector);

        Context = context;
        _selector = selector;
    }

    protected MemoryPersistenceContext Context { get; }

    public virtual ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var candidate in Context.Set<TAggregateRoot>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_selector(candidate, keyValues))
            {
                return ValueTask.FromResult<TAggregateRoot?>(candidate);
            }
        }

        return ValueTask.FromResult<TAggregateRoot?>(null);
    }

    public virtual async IAsyncEnumerable<TAggregateRoot> FindAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var snapshot = Context.Set<TAggregateRoot>();

        foreach (var entity in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return entity;
            await Task.CompletedTask;
        }
    }

    public virtual void Add(TAggregateRoot entity)
        => Context.Set<TAggregateRoot>().Add(entity);

    public virtual void Remove(TAggregateRoot entity)
    {
        var table = Context.Set<TAggregateRoot>();
        for (var i = table.Count - 1; i >= 0; i--)
        {
            var candidate = table[i];
            if (ReferenceEquals(candidate, entity) || EqualityComparer<TAggregateRoot>.Default.Equals(candidate, entity))
            {
                table.RemoveAt(i);
            }
        }
    }
}

public abstract class MemoryRepository<TAggregateRoot, TKey> : MemoryRepository<TAggregateRoot>, IRepository<TAggregateRoot, TKey>
    where TAggregateRoot : class, IAggregateRoot, ICloneable
    where TKey : notnull
{
    protected MemoryRepository(
        MemoryPersistenceContext context,
        Func<TAggregateRoot, TKey> keySelector)
        : base(
            context,
            (entity, keyValues) =>
                keyValues is [TKey key] && EqualityComparer<TKey>.Default.Equals(keySelector(entity), key))
    {
        ArgumentNullException.ThrowIfNull(keySelector);
    }
}

public abstract class MemoryRepository<TAggregateRoot, TKey1, TKey2> : MemoryRepository<TAggregateRoot>, IRepository<TAggregateRoot, TKey1, TKey2>
    where TAggregateRoot : class, IAggregateRoot, ICloneable
    where TKey1 : notnull
    where TKey2 : notnull
{
    protected MemoryRepository(
        MemoryPersistenceContext context,
        Func<TAggregateRoot, (TKey1, TKey2)> keySelector)
        : base(
            context,
            (entity, keyValues) =>
                keyValues is [TKey1 key1, TKey2 key2] &&
                EqualityComparer<(TKey1, TKey2)>.Default.Equals(keySelector(entity), (key1, key2)))
    {
        ArgumentNullException.ThrowIfNull(keySelector);
    }
}

public abstract class MemoryRepository<TAggregateRoot, TKey1, TKey2, TKey3> : MemoryRepository<TAggregateRoot>, IRepository<TAggregateRoot, TKey1, TKey2, TKey3>
    where TAggregateRoot : class, IAggregateRoot, ICloneable
    where TKey1 : notnull
    where TKey2 : notnull
    where TKey3 : notnull
{
    protected MemoryRepository(
        MemoryPersistenceContext context,
        Func<TAggregateRoot, (TKey1, TKey2, TKey3)> keySelector)
        : base(
            context,
            (entity, keyValues) =>
                keyValues is [TKey1 key1, TKey2 key2, TKey3 key3] &&
                EqualityComparer<(TKey1, TKey2, TKey3)>.Default.Equals(keySelector(entity), (key1, key2, key3)))
    {
        ArgumentNullException.ThrowIfNull(keySelector);
    }
}
