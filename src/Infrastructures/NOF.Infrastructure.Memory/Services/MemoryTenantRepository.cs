using Microsoft.Extensions.Logging;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryTenantRepository : MemoryRepository<NOFTenant, string>, ITenantRepository
{
    private readonly ILogger<MemoryTenantRepository> _logger;

    public MemoryTenantRepository(MemoryPersistenceStore store, MemoryPersistenceSession session, ILogger<MemoryTenantRepository> logger)
        : base(store, session, "nof:tenant", static tenant => tenant.Id, static tenant => new NOFTenant
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Description = tenant.Description,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt
        }, StringComparer.OrdinalIgnoreCase)
    {
        _logger = logger;
    }

    public ValueTask<bool> ExistsAsync(string tenantId)
    {
        var exists = !string.IsNullOrWhiteSpace(tenantId) && Items.ContainsKey(tenantId);
        _logger.LogDebug("Checked existence of tenant {TenantId}: {Exists}", tenantId, exists);
        return ValueTask.FromResult(exists);
    }

    protected override IEnumerable<NOFTenant> OrderItems(IEnumerable<NOFTenant> items)
        => items.OrderBy(tenant => tenant.Id, StringComparer.OrdinalIgnoreCase);
}
