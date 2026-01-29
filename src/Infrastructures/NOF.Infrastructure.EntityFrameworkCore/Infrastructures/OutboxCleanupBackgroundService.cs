using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NOF;

/// <summary>
/// Outbox 清理服务
/// 定期清理已发送的旧命令，维持数据库性能
/// </summary>
internal sealed class OutboxCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxCleanupBackgroundService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(7);

    public OutboxCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OutboxCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Outbox cleanup service started. Cleanup interval: {Interval}, Retention period: {Retention}",
            _cleanupInterval, _retentionPeriod);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                await CleanupOutboxAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during outbox cleanup");
            }
        }

        _logger.LogInformation("Outbox cleanup service stopped");
    }

    private async Task CleanupOutboxAsync(CancellationToken cancellationToken)
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
                _logger.LogDebug("Cleaning outbox messages for tenant {TenantId}", tenant.Id);

                // 使用当前 scope 的 repository，它会自动使用设置的租户上下文
                var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

                var olderThan = DateTimeOffset.UtcNow - _retentionPeriod;
                var deletedCount = await dbContext.Set<EFCoreOutboxMessage>()
                    .Where(m => m.Status == OutboxMessageStatus.Sent)
                    .Where(m => m.SentAt != null && m.SentAt < olderThan)
                    .ExecuteDeleteAsync(cancellationToken);

                if (deletedCount > 0)
                {
                    _logger.LogInformation(
                        "Tenant {TenantId} outbox cleanup completed. Deleted {Count} sent messages older than {Date}",
                        tenant.Id, deletedCount, olderThan);
                }
                else
                {
                    _logger.LogDebug("Tenant {TenantId} outbox cleanup completed. No messages to delete", tenant.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning outbox messages for tenant {TenantId}", tenant.Id);
            }
        }

        // 恢复原始租户上下文
        tenantContext.SetCurrentTenantId(originalTenantId);
    }
}
