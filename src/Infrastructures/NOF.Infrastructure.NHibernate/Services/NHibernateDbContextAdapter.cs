using NHibernate;
using NOF.Application;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NOF.Infrastructure.NHibernate;

internal sealed class NHibernateDbContextAdapter(ISession session) : IDbContext
{
    private readonly ISession _session = session;
    private readonly NHibernateAsyncQueryExecutor _asyncExecutor = new(session);

    public IDbSet<TEntity> Set<TEntity>()
        where TEntity : class
        => new NHibernateDbSetAdapter<TEntity>(_session, _asyncExecutor);

    public int SaveChanges()
        => SaveChanges(acceptAllChangesOnSuccess: true);

    public int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        _ = acceptAllChangesOnSuccess;

        try
        {
            _session.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            throw NHibernateExceptionTranslator.TranslateSaveChangesException(ex);
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SaveChanges(acceptAllChangesOnSuccess));
    }

    public IDbContextTransaction BeginTransaction()
    {
        try
        {
            return new NHibernateDbContextTransactionAdapter(_session.BeginTransaction());
        }
        catch (Exception ex)
        {
            throw NHibernateExceptionTranslator.TranslateTransactionException(ex, "Failed to begin a database transaction.");
        }
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BeginTransaction());
    }
}

internal sealed class NHibernateDbContextTransactionAdapter(ITransaction transaction) : IDbContextTransaction
{
    private readonly ITransaction _transaction = transaction;

    public Guid TransactionId { get; } = Guid.NewGuid();

    public void Commit()
    {
        try
        {
            _transaction.Commit();
        }
        catch (Exception ex)
        {
            throw NHibernateExceptionTranslator.TranslateCommitException(ex);
        }
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Commit();
        return Task.CompletedTask;
    }

    public void Rollback()
    {
        try
        {
            _transaction.Rollback();
        }
        catch (Exception ex)
        {
            throw NHibernateExceptionTranslator.TranslateTransactionException(ex, "Failed to roll back the database transaction.");
        }
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Rollback();
        return Task.CompletedTask;
    }

    public void CreateSavepoint(string name)
        => throw new NotSupportedException("NHibernate transaction savepoints are not implemented by NOF.Infrastructure.NHibernate yet.");

    public Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CreateSavepoint(name);
        return Task.CompletedTask;
    }

    public void RollbackToSavepoint(string name)
        => throw new NotSupportedException("NHibernate transaction savepoints are not implemented by NOF.Infrastructure.NHibernate yet.");

    public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RollbackToSavepoint(name);
        return Task.CompletedTask;
    }

    public void ReleaseSavepoint(string name)
        => throw new NotSupportedException("NHibernate transaction savepoints are not implemented by NOF.Infrastructure.NHibernate yet.");

    public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReleaseSavepoint(name);
        return Task.CompletedTask;
    }

    public void Dispose()
        => _transaction.Dispose();

    public ValueTask DisposeAsync()
    {
        _transaction.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed class NHibernateDbSetAdapter<TEntity> : AsyncQueryable<TEntity>, IDbSet<TEntity>
    where TEntity : class
{
    private readonly ISession _session;

    public NHibernateDbSetAdapter(ISession session, IAsyncQueryExecutor asyncExecutor)
        : base(session.Query<TEntity>(), asyncExecutor)
    {
        _session = session;
    }

    public void Add(TEntity entity)
        => _session.Save(entity);

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
        => _session.Lock(entity, LockMode.None);

    public void AttachRange(IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
        {
            Attach(entity);
        }
    }

    public void Update(TEntity entity)
        => _session.SaveOrUpdate(entity);

    public void UpdateRange(IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
        {
            Update(entity);
        }
    }

    public void Remove(TEntity entity)
        => _session.Delete(entity);

    public void RemoveRange(IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
        {
            Remove(entity);
        }
    }

    public IAsyncQueryable<TEntity> AsNoTracking()
        => new AsyncQueryable<TEntity>(_session.Query<TEntity>(), AsyncExecutor);
}

internal sealed class NHibernateAsyncQueryExecutor(ISession session) : IAsyncQueryExecutor
{
    private static readonly ConcurrentDictionary<string, Delegate> PropertySetters = new(StringComparer.Ordinal);
    private readonly ISession _session = session;

    public async IAsyncEnumerable<TSource> AsAsyncEnumerable<TSource>(IQueryable<TSource> source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var list = await ToListAsync(source, cancellationToken);
        foreach (var item in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    public Task<int> ExecuteDeleteAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = Unwrap(source).ToList();
        foreach (var item in items)
        {
            _session.Delete(item!);
        }

        _session.Flush();
        return Task.FromResult(items.Count);
    }

    public Task<int> ExecuteUpdateAsync<TSource>(IQueryable<TSource> source, IUpdateSetters<TSource> setters, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(setters);

        var items = Unwrap(source).ToList();
        foreach (var item in items)
        {
            ApplySetters(item, setters.SetPropertyCalls);
        }

        _session.Flush();
        return Task.FromResult(items.Count);
    }

    public Task<List<TSource>> ToListAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Unwrap(source).ToList());
    }

    public Task<TSource[]> ToArrayAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Unwrap(source).ToArray());
    }

    public Task LoadAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = Unwrap(source).ToList();
        return Task.CompletedTask;
    }

    public Task<bool> AnyAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Any());
    public Task<bool> AllAsync<TSource>(IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).All(predicate.Compile()));
    public Task<bool> ContainsAsync<TSource>(IQueryable<TSource> source, TSource value, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Contains(value));
    public Task<int> CountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Count());
    public Task<long> LongCountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).LongCount());
    public Task<TSource> FirstAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).First());
    public Task<TSource?> FirstOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).FirstOrDefault());
    public Task<TSource> SingleAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Single());
    public Task<TSource?> SingleOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).SingleOrDefault());
    public Task<TSource> LastAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).ToList().Last());
    public Task<TSource?> LastOrDefaultAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).ToList().LastOrDefault());
    public Task<TSource> ElementAtAsync<TSource>(IQueryable<TSource> source, int index, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).ElementAt(index));
    public Task<TSource?> ElementAtOrDefaultAsync<TSource>(IQueryable<TSource> source, int index, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).ElementAtOrDefault(index));
    public Task<TSource> MinAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Min()!);
    public Task<TSource> MaxAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Max()!);
    public Task<int> SumAsync(IQueryable<int> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Sum());
    public Task<int?> SumAsync(IQueryable<int?> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Sum());
    public Task<long> SumAsync(IQueryable<long> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Sum());
    public Task<long?> SumAsync(IQueryable<long?> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Sum());
    public Task<float> SumAsync(IQueryable<float> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Sum());
    public Task<float?> SumAsync(IQueryable<float?> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Sum());
    public Task<double> SumAsync(IQueryable<double> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Sum());
    public Task<double?> SumAsync(IQueryable<double?> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Sum());
    public Task<decimal> SumAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Sum());
    public Task<decimal?> SumAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Sum());
    public Task<double> AverageAsync(IQueryable<int> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Average());
    public Task<double?> AverageAsync(IQueryable<int?> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Average());
    public Task<double> AverageAsync(IQueryable<long> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Average());
    public Task<double?> AverageAsync(IQueryable<long?> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Average());
    public Task<float> AverageAsync(IQueryable<float> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Average());
    public Task<float?> AverageAsync(IQueryable<float?> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Average());
    public Task<double> AverageAsync(IQueryable<double> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Average());
    public Task<double?> AverageAsync(IQueryable<double?> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Average());
    public Task<decimal> AverageAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Average());
    public Task<decimal?> AverageAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default) => Task.FromResult(Unwrap(source).Average());

    private static IQueryable<TSource> Unwrap<TSource>(IQueryable<TSource> source)
        => source is IAsyncQueryableAccessor accessor
            ? (IQueryable<TSource>)accessor.Query
            : source;

    private static void ApplySetters<TSource>(TSource entity, IReadOnlyList<UpdateSetPropertyCall<TSource>> setters)
    {
        foreach (var setter in setters)
        {
            ApplySetter(entity, setter);
        }
    }

    private static void ApplySetter<TSource>(TSource entity, UpdateSetPropertyCall<TSource> setter)
    {
        var propertyLambda = (LambdaExpression)setter.PropertyExpression;
        var propertyInfo = ExtractPropertyInfo(propertyLambda);
        var setterDelegate = GetSetter<TSource>(propertyInfo);

        var value = setter switch
        {
            ConstantUpdateSetPropertyCall<TSource> constant => constant.Value,
            ComputedUpdateSetPropertyCall<TSource> computed => computed.ValueExpression.Compile().DynamicInvoke(entity),
            _ => throw new NotSupportedException($"Unsupported update setter type '{setter.GetType().FullName}'.")
        };

        setterDelegate(entity, value);
    }

    private static Action<TSource, object?> GetSetter<TSource>(PropertyInfo propertyInfo)
    {
        var cacheKey = $"{typeof(TSource).AssemblyQualifiedName}|{propertyInfo.Name}";
        var setter = PropertySetters.GetOrAdd(cacheKey, _ =>
        {
            var entity = Expression.Parameter(typeof(TSource), "entity");
            var value = Expression.Parameter(typeof(object), "value");
            var assign = Expression.Assign(
                Expression.Property(entity, propertyInfo),
                Expression.Convert(value, propertyInfo.PropertyType));

            return Expression.Lambda<Action<TSource, object?>>(assign, entity, value).Compile();
        });

        return (Action<TSource, object?>)setter;
    }

    private static PropertyInfo ExtractPropertyInfo(LambdaExpression expression)
        => expression.Body switch
        {
            MemberExpression { Member: PropertyInfo propertyInfo } => propertyInfo,
            UnaryExpression { Operand: MemberExpression { Member: PropertyInfo propertyInfo } } => propertyInfo,
            _ => throw new NotSupportedException($"Unsupported property expression '{expression.Body.NodeType}'.")
        };
}
