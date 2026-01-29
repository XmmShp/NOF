using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NOF;

/// <summary>
/// Inbox 清理服务
/// 定期清理已处理的旧消息，维持数据库性能
/// </summary>
internal sealed class InboxCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxCleanupBackgroundService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(7);

    public InboxCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<InboxCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Inbox cleanup service started. Cleanup interval: {Interval}, Retention period: {Retention}",
            _cleanupInterval, _retentionPeriod);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                await CleanupInboxAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during inbox cleanup");
            }
        }

        _logger.LogInformation("Inbox cleanup service stopped");
    }

    private async Task CleanupInboxAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextInternal>();

        // 获取所有租户
        var tenants = await tenantRepository.GetAllAsync();
        
        // 保存原始租户上下文
        var originalTenantId = tenantContext.CurrentTenantId;
        
        foreach (var tenant in tenants)
        {
            if (!tenant.IsActive)
            {
                _logger.LogDebug("Skipping inactive tenant {TenantId}", tenant.Id);
                continue;
            }

            try
            {
                // 设置租户上下文
                tenantContext.SetCurrentTenantId(tenant.Id);
                _logger.LogDebug("Cleaning inbox messages for tenant {TenantId}", tenant.Id);

                // 使用当前 scope 的 repository，它会自动使用设置的租户上下文
                var dbContext = scope.ServiceProvider.GetRequiredService<NOFDbContext>();

                var olderThan = DateTime.UtcNow - _retentionPeriod;
                var deletedCount = await dbContext.Set<EFCoreInboxMessage>()
                    .Where(m => m.CreatedAt < olderThan)
                    .ExecuteDeleteAsync(cancellationToken);

                if (deletedCount > 0)
                {
                    _logger.LogInformation(
                        "Tenant {TenantId} inbox cleanup completed. Deleted {Count} processed messages older than {Date}",
                        tenant.Id, deletedCount, olderThan);
                }
                else
                {
                    _logger.LogDebug("Tenant {TenantId} inbox cleanup completed. No messages to delete", tenant.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning inbox messages for tenant {TenantId}", tenant.Id);
            }
        }

        // 恢复原始租户上下文
        tenantContext.SetCurrentTenantId(originalTenantId);
    }
}
