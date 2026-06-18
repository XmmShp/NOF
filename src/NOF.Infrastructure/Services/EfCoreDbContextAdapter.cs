using Microsoft.EntityFrameworkCore;
using NOF.Application;
using System.Collections;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NOF.Infrastructure;

internal sealed class EfCoreDbContextAdapter(DbContext dbContext) : IDbContext
{
    private readonly DbContext _dbContext = dbContext;

    public IDbSet<TEntity> Set<TEntity>()
        where TEntity : class
        => new EfCoreDbSetAdapter<TEntity>(_dbContext.Set<TEntity>());

    public int SaveChanges()
        => _dbContext.SaveChanges();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}

internal sealed class EfCoreDbSetAdapter<TEntity>(DbSet<TEntity> dbSet) : IDbSet<TEntity>
    where TEntity : class
{
    private readonly DbSet<TEntity> _dbSet = dbSet;

    public Type ElementType => ((IQueryable<TEntity>)_dbSet).ElementType;

    public Expression Expression => ((IQueryable<TEntity>)_dbSet).Expression;

    public IQueryProvider Provider => ((IQueryable<TEntity>)_dbSet).Provider;

    public void Add(TEntity entity)
        => _dbSet.Add(entity);

    public void Attach(TEntity entity)
        => _dbSet.Attach(entity);

    public void Update(TEntity entity)
        => _dbSet.Update(entity);

    public void Remove(TEntity entity)
        => _dbSet.Remove(entity);

    public IQueryable<TEntity> AsNoTracking()
        => _dbSet.AsNoTracking();

    public IEnumerator<TEntity> GetEnumerator()
        => ((IQueryable<TEntity>)_dbSet).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (_dbSet is IAsyncEnumerable<TEntity> asyncEnumerable)
        {
            return asyncEnumerable.GetAsyncEnumerator(cancellationToken);
        }

        return EnumerateSync(_dbSet, cancellationToken).GetAsyncEnumerator(cancellationToken);
    }

    private static async IAsyncEnumerable<TEntity> EnumerateSync(
        IEnumerable<TEntity> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await Task.CompletedTask;
        }
    }
}
