using Microsoft.EntityFrameworkCore;

namespace NOF;

public abstract class Repository<TAggregateRoot> : IRepository<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    protected readonly DbContext DbContext;
    protected Repository(DbContext dbContext)
    {
        DbContext = dbContext;
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

public abstract class Repository<TAggregateRoot, TKey> : Repository<TAggregateRoot>, IRepository<TAggregateRoot, TKey>
    where TKey : struct
    where TAggregateRoot : class, IAggregateRoot<TKey>
{
    protected Repository(DbContext dbContext) : base(dbContext)
    {
    }

    public virtual ValueTask<TAggregateRoot?> FindAsync(TKey key, CancellationToken cancellationToken)
    {
        return DbContext.Set<TAggregateRoot>().FindAsync([key], cancellationToken);
    }
}