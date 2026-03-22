using Microsoft.Extensions.Logging;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryTenantRepository : MemoryRepository<NOFTenant, string>, ITenantRepository
{
    private readonly ILogger<MemoryTenantRepository> _logger;

    public MemoryTenantRepository(MemoryPersistenceContext context, ILogger<MemoryTenantRepository> logger)
        : base(context, static tenant => tenant.Id)
    {
        _logger = logger;
    }

    public ValueTask<bool> ExistsAsync(string tenantId)
    {
        var exists = Context.Set<NOFTenant>().Any(tenant => tenant.Id == tenantId);
        _logger.LogDebug("Checked existence of tenant {TenantId}: {Exists}", tenantId, exists);
        return ValueTask.FromResult(exists);
    }
}
