using NOF.Domain;

namespace NOF.Infrastructure.EntityFrameworkCore;

public abstract class EFCoreRepository<TAggregateRoot> : IRepository<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    protected readonly NOFDbContext DbContext;
    protected EFCoreRepository(NOFDbContext dbContext)
    {
        DbContext = dbContext;
    }

    public virtual ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken cancellationToken)
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
