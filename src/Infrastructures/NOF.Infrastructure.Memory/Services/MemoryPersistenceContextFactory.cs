using NOF.Application;

namespace NOF.Infrastructure.Memory;

public interface IMemoryPersistenceContextFactory
{
    MemoryPersistenceContext CreateContext();

    MemoryPersistenceContext CreateContext(string? tenantId);
}

public sealed class MemoryPersistenceContextFactory : IMemoryPersistenceContextFactory
{
    private readonly MemoryPersistenceStore _store;
    private readonly IInvocationContext _invocationContext;

    public MemoryPersistenceContextFactory(MemoryPersistenceStore store, IInvocationContext invocationContext)
    {
        _store = store;
        _invocationContext = invocationContext;
    }

    public MemoryPersistenceContext CreateContext()
        => CreateContext(_invocationContext.TenantId);

    public MemoryPersistenceContext CreateContext(string? tenantId)
        => _store.CreateContext(tenantId);
}
