using NOF.Application;

namespace NOF.Infrastructure.Memory;

public interface IMemoryPersistenceContextFactory
{
    MemoryPersistenceContext CreateContext();

    MemoryPersistenceContext CreateContext(string tenantId);
}

public sealed class MemoryPersistenceContextFactory : IMemoryPersistenceContextFactory
{
    private readonly MemoryPersistenceStore _store;
    private readonly IExecutionContext _executionContext;

    public MemoryPersistenceContextFactory(MemoryPersistenceStore store, IExecutionContext executionContext)
    {
        _store = store;
        _executionContext = executionContext;
    }

    public MemoryPersistenceContext CreateContext()
        => CreateContext(_executionContext.TenantId);

    public MemoryPersistenceContext CreateContext(string tenantId)
        => _store.CreateContext(tenantId);
}
