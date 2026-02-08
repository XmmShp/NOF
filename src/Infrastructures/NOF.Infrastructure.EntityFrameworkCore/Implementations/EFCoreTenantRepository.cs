using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NOF;

/// <summary>
/// EF Core tenant repository implementation.
/// </summary>
internal sealed class EFCoreTenantRepository : ITenantRepository
{
    private readonly NOFDbContext _dbContext;
    private readonly ILogger<EFCoreTenantRepository> _logger;

    public EFCoreTenantRepository(NOFDbContext dbContext, ILogger<EFCoreTenantRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<Tenant?> FindAsync(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        var entity = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (entity is null)
        {
            return null;
        }

        _logger.LogDebug("Found tenant {TenantId}", tenantId);
        return new Tenant
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async ValueTask<IReadOnlyList<Tenant>> GetAllAsync()
    {
        var entities = await _dbContext.Tenants
            .AsNoTracking()
            .ToListAsync();

        _logger.LogDebug("Retrieved {Count} tenants", entities.Count);
        return entities.Select(e => new Tenant
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            IsActive = e.IsActive,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        }).ToList();
    }

    public void Add(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        var entity = new EFCoreTenant
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Description = tenant.Description,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt
        };

        _dbContext.Tenants.Add(entity);
        _logger.LogDebug("Added tenant {TenantId}", tenant.Id);
    }

    public void Update(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        var entity = new EFCoreTenant
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Description = tenant.Description,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Update(entity);
        _logger.LogDebug("Updated tenant {TenantId}", tenant.Id);
    }

    public void Delete(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return;
        }

        var entity = new EFCoreTenant { Id = tenantId };
        _dbContext.Tenants.Attach(entity);
        _dbContext.Tenants.Remove(entity);
        _logger.LogDebug("Deleted tenant {TenantId}", tenantId);
    }

    public void Remove(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        Delete(tenant.Id);
    }

    public async ValueTask<bool> ExistsAsync(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        var exists = await _dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantId);

        _logger.LogDebug("Checked existence of tenant {TenantId}: {Exists}", tenantId, exists);
        return exists;
    }
}
