using NOF.Domain;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

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

    protected virtual IQueryable<TAggregateRoot> QueryableSource => CreateTrackingQueryable();

    public Type ElementType => QueryableSource.ElementType;

    public Expression Expression => QueryableSource.Expression;

    public IQueryProvider Provider => QueryableSource.Provider;

    public IEnumerator<TAggregateRoot> GetEnumerator() => QueryableSource.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public virtual IQueryable<TAggregateRoot> AsNoTracking() => CreateNoTrackingQueryable();

    public virtual ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var candidate in Context.Set<TAggregateRoot>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_selector(candidate, keyValues))
            {
                Context.TrackEntity(candidate);
                return ValueTask.FromResult<TAggregateRoot?>(candidate);
            }
        }

        return ValueTask.FromResult<TAggregateRoot?>(null);
    }

    public virtual async IAsyncEnumerable<TAggregateRoot> FindAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var entity in Context.Set<TAggregateRoot>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            Context.TrackEntity(entity);
            yield return entity;
            await Task.CompletedTask;
        }
    }

    public virtual IQueryable<TAggregateRoot> FromSql(FormattableString sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        throw CreateRawSqlNotSupportedException();
    }

    public virtual IQueryable<TAggregateRoot> FromSqlRaw(string sql, params object?[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        throw CreateRawSqlNotSupportedException();
    }

    public virtual Task<int> ExecuteSqlAsync(FormattableString sql, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sql);
        throw CreateRawSqlNotSupportedException();
    }

    public virtual Task<int> ExecuteSqlRawAsync(string sql, object?[]? parameters = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        throw CreateRawSqlNotSupportedException();
    }

    public virtual void Add(TAggregateRoot entity)
    {
        Context.Set<TAggregateRoot>().Add(entity);
        Context.TrackEntity(entity);
    }

    public virtual void Remove(TAggregateRoot entity)
    {
        var table = Context.Set<TAggregateRoot>();
        var removed = false;
        for (var i = table.Count - 1; i >= 0; i--)
        {
            var candidate = table[i];
            if (ReferenceEquals(candidate, entity) || EqualityComparer<TAggregateRoot>.Default.Equals(candidate, entity))
            {
                table.RemoveAt(i);
                removed = true;
            }
        }

        if (removed)
        {
            Context.TrackEntity(entity);
        }
    }

    protected IEnumerable<TAggregateRoot> Track(IEnumerable<TAggregateRoot> entities)
    {
        foreach (var entity in entities)
        {
            Context.TrackEntity(entity);
            yield return entity;
        }
    }

    protected IEnumerable<TAggregateRoot> Detach(IEnumerable<TAggregateRoot> entities)
    {
        foreach (var entity in entities)
        {
            yield return (TAggregateRoot)entity.Clone();
        }
    }

    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "In-memory IQueryable support is limited to the memory provider and intentionally uses EnumerableQuery.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "In-memory IQueryable support is limited to the memory provider and intentionally uses EnumerableQuery.")]
    protected virtual IQueryable<TAggregateRoot> CreateTrackingQueryable()
        => new EnumerableQuery<TAggregateRoot>(Track(Context.Set<TAggregateRoot>()));

    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "In-memory IQueryable support is limited to the memory provider and intentionally uses EnumerableQuery.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "In-memory IQueryable support is limited to the memory provider and intentionally uses EnumerableQuery.")]
    protected virtual IQueryable<TAggregateRoot> CreateNoTrackingQueryable()
        => new EnumerableQuery<TAggregateRoot>(Detach(Context.Set<TAggregateRoot>()));

    private NotSupportedException CreateRawSqlNotSupportedException()
        => new($"{GetType().Name} does not support raw SQL operations.");
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
