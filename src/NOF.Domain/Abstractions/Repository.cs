namespace NOF;

public interface IRepository;

public interface IRepository<TAggregateRoot> : IRepository
    where TAggregateRoot : class, IAggregateRoot
{
    void Add(TAggregateRoot entity);
    void Remove(TAggregateRoot entity);
}

public interface IRepository<TAggregateRoot, TKey> : IRepository<TAggregateRoot>
    where TKey : struct
    where TAggregateRoot : class, IAggregateRoot<TKey>
{
    ValueTask<TAggregateRoot?> FindAsync(TKey key, CancellationToken cancellationToken = default);
}