namespace NOF;

public interface IDataAccessContext
{
    IUnitOfWork UnitOfWork { get; }
    ITransactionManager TransactionManager { get; }
    TRepository GetRepository<TRepository>() where TRepository : class;
}

public interface IDataAccessContext<TRepository> where TRepository : class
{
    IUnitOfWork UnitOfWork { get; }
    ITransactionManager TransactionManager { get; }
    TRepository Repository { get; }
}

public class DataAccessContextWrapper<TRepository> : IDataAccessContext<TRepository>
    where TRepository : class
{
    private readonly IDataAccessContext _dataAccessContext;
    public DataAccessContextWrapper(IDataAccessContext dataAccessContext)
    {
        _dataAccessContext = dataAccessContext;
    }

    public IUnitOfWork UnitOfWork => _dataAccessContext.UnitOfWork;
    public ITransactionManager TransactionManager => _dataAccessContext.TransactionManager;
    public TRepository Repository => _dataAccessContext.GetRepository<TRepository>();
}