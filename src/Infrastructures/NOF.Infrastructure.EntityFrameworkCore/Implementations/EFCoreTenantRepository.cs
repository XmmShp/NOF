using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// EF Core tenant repository implementation.
/// </summary>
internal sealed class EFCoreTenantRepository : EFCoreRepository<NOFDbContext, NOFTenant>, ITenantRepository
{
    private readonly ILogger<EFCoreTenantRepository> _logger;

    public EFCoreTenantRepository(NOFDbContext dbContext, ILogger<EFCoreTenantRepository> logger) : base(dbContext)
    {
        _logger = logger;
    }

    public async ValueTask<bool> ExistsAsync(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        var exists = await DbContext.NOFTenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantId);

        _logger.LogDebug("Checked existence of tenant {TenantId}: {Exists}", tenantId, exists);
        return exists;
    }
}
