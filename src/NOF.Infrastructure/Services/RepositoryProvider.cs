using NOF.Application;
using NOF.Domain;
using System.Collections;
using System.Linq.Expressions;

namespace NOF.Infrastructure;

internal sealed class RepositoryProvider<TEntity>(IDbContext dbContext) : IRepository<TEntity>
    where TEntity : class
{
    private IRepository<TEntity> Repository => dbContext.Set<TEntity>();

    public IAsyncQueryExecutor AsyncExecutor => Repository.AsyncExecutor;

    public Type ElementType => Repository.ElementType;

    public Expression Expression => Repository.Expression;

    public IQueryProvider Provider => Repository.Provider;

    public int Count => Repository.Count;

    public bool IsReadOnly => Repository.IsReadOnly;

    public void Add(TEntity entity)
        => Repository.Add(entity);

    public ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => Repository.AddAsync(entity, cancellationToken);

    public void AddRange(IEnumerable<TEntity> entities)
        => Repository.AddRange(entities);

    public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        => Repository.AddRangeAsync(entities, cancellationToken);

    public void Attach(TEntity entity)
        => Repository.Attach(entity);

    public void AttachRange(IEnumerable<TEntity> entities)
        => Repository.AttachRange(entities);

    public void Update(TEntity entity)
        => Repository.Update(entity);

    public void UpdateRange(IEnumerable<TEntity> entities)
        => Repository.UpdateRange(entities);

    public bool Remove(TEntity entity)
        => Repository.Remove(entity);

    public void RemoveRange(IEnumerable<TEntity> entities)
        => Repository.RemoveRange(entities);

    public void Clear()
        => Repository.Clear();

    public bool Contains(TEntity item)
        => Repository.Contains(item);

    public void CopyTo(TEntity[] array, int arrayIndex)
        => Repository.CopyTo(array, arrayIndex);

    public IAsyncQueryable<TEntity> AsNoTracking()
        => Repository.AsNoTracking();

    public IEnumerator<TEntity> GetEnumerator()
        => Repository.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
