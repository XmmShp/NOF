namespace NOF;

public interface IRepositoryBundle<out TRepository>
    where TRepository : IRepository
{
    /// <summary>
    /// Unit of work for managing transactions
    /// </summary>
    IUnitOfWork UnitOfWork { get; }

    /// <summary>
    /// Transaction manager for handling transaction operations
    /// </summary>
    ITransactionManager TransactionManager { get; }

    /// <summary>
    /// Database context factory for creating contexts
    /// </summary>
    TRepository Repository { get; }
}

/// <summary>
/// Repository factory bundle that provides all necessary data access components
/// </summary>
public sealed class RepositoryBundle<TRepository> : IRepositoryBundle<TRepository>
    where TRepository : IRepository
{
    /// <summary>
    /// Unit of work for managing transactions
    /// </summary>
    public IUnitOfWork UnitOfWork { get; init; }

    /// <summary>
    /// Transaction manager for handling transaction operations
    /// </summary>
    public ITransactionManager TransactionManager { get; init; }

    /// <summary>
    /// Database context factory for creating contexts
    /// </summary>
    public TRepository Repository { get; init; }
}