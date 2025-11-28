namespace NOF;

public interface IRepository;

public interface IRepository<TAggregateRoot> : IRepository
    where TAggregateRoot : class, IAggregateRoot
{
    ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken cancellationToken = default);
    void Add(TAggregateRoot entity);
    void Remove(TAggregateRoot entity);
}