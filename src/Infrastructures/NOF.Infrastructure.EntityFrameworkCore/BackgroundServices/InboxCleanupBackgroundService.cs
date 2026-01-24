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
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NOFDbContext>();

            var olderThan = DateTime.UtcNow - _retentionPeriod;
            var deletedCount = await dbContext.Set<EFCoreInboxMessage>()
                .Where(m => m.CreatedAt < olderThan)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Inbox cleanup completed. Deleted {Count} processed messages older than {Date}",
                    deletedCount, olderThan);
            }
            else
            {
                _logger.LogDebug("Inbox cleanup completed. No messages to delete");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup inbox");
        }
    }
}
