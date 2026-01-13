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

    public ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken cancellationToken)
    {
        return DbContext.Set<TAggregateRoot>().FindAsync(keyValues: keyValues, cancellationToken: cancellationToken);
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