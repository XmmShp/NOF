using Microsoft.EntityFrameworkCore;
using NOF.Domain;
using System.Collections;
using System.Linq.Expressions;

namespace NOF.Infrastructure.EntityFrameworkCore;

public abstract class EFCoreRepository<TAggregateRoot> : IRepository<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    protected readonly DbContext DbContext;
    protected EFCoreRepository(DbContext dbContext)
    {
        DbContext = dbContext;
    }

    protected virtual IQueryable<TAggregateRoot> QueryableSource => DbContext.Set<TAggregateRoot>();

    public Type ElementType => QueryableSource.ElementType;

    public Expression Expression => QueryableSource.Expression;

    public IQueryProvider Provider => QueryableSource.Provider;

    public IEnumerator<TAggregateRoot> GetEnumerator() => QueryableSource.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public virtual IQueryable<TAggregateRoot> AsNoTracking() => DbContext.Set<TAggregateRoot>().AsNoTracking();

    public virtual ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken cancellationToken)
    {
        return DbContext.FindAsync<TAggregateRoot>(keyValues, cancellationToken);
    }

    public virtual IAsyncEnumerable<TAggregateRoot> FindAllAsync(CancellationToken cancellationToken = default)
    {
        return DbContext.Set<TAggregateRoot>().AsAsyncEnumerable();
    }

    public virtual IQueryable<TAggregateRoot> FromSql(FormattableString sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return DbContext.Set<TAggregateRoot>().FromSql(sql);
    }

    public virtual IQueryable<TAggregateRoot> FromSqlRaw(string sql, params object?[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        return DbContext.Set<TAggregateRoot>().FromSqlRaw(sql, parameters);
    }

    public virtual Task<int> ExecuteSqlAsync(FormattableString sql, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return DbContext.Database.ExecuteSqlAsync(sql, cancellationToken);
    }

    public virtual Task<int> ExecuteSqlRawAsync(string sql, object?[]? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        return DbContext.Database.ExecuteSqlRawAsync(sql, parameters?.Cast<object>().ToArray() ?? [], cancellationToken);
    }

    public virtual void Add(TAggregateRoot aggregateRoot)
    {
        DbContext.Set<TAggregateRoot>().Add(aggregateRoot);
    }

    public virtual void Remove(TAggregateRoot aggregateRoot)
    {
        DbContext.Set<TAggregateRoot>().Remove(aggregateRoot);
    }
}

public abstract class EFCoreRepository<TDbContext, TAggregateRoot> : EFCoreRepository<TAggregateRoot>
    where TDbContext : DbContext
    where TAggregateRoot : class, IAggregateRoot
{
    protected new readonly TDbContext DbContext;
    protected EFCoreRepository(TDbContext dbContext) : base(dbContext)
    {
        DbContext = dbContext;
    }
}
