using Microsoft.EntityFrameworkCore;
using NOF.Domain;

namespace NOF.Infrastructure.EntityFrameworkCore;

public abstract class EFCoreRepository<TAggregateRoot> : IRepository<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    protected readonly DbContext DbContext;
    protected EFCoreRepository(DbContext dbContext)
    {
        DbContext = dbContext;
    }

    public virtual ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken cancellationToken)
    {
        return DbContext.Set<TAggregateRoot>().FindAsync(keyValues: keyValues, cancellationToken: cancellationToken);
    }

    public virtual IAsyncEnumerable<TAggregateRoot> FindAllAsync(CancellationToken cancellationToken = default)
    {
        return DbContext.Set<TAggregateRoot>().AsAsyncEnumerable();
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
