namespace NOF;

public static class RepositoryExtensions
{
    extension<TAggregateRoot>(IRepository<TAggregateRoot> repository)
        where TAggregateRoot : class, IAggregateRoot
    {
        public ValueTask<TAggregateRoot?> FindAsync(
            object? key,
            CancellationToken cancellationToken = default)
            => repository.FindAsync(keyValues: [key], cancellationToken: cancellationToken);

        public ValueTask<TAggregateRoot?> FindAsync(
            object? key1,
            object? key2,
            CancellationToken cancellationToken = default)
            => repository.FindAsync(keyValues: [key1, key2], cancellationToken: cancellationToken);

        public ValueTask<TAggregateRoot?> FindAsync(
            object? key1,
            object? key2,
            object? key3,
            CancellationToken cancellationToken = default)
            => repository.FindAsync(keyValues: [key1, key2, key3], cancellationToken: cancellationToken);
    }
}
