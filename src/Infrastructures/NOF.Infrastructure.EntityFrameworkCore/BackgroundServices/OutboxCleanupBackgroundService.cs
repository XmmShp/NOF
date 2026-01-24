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
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

            var olderThan = DateTimeOffset.UtcNow - _retentionPeriod;
            var deletedCount = await dbContext.Set<EFCoreOutboxMessage>()
                .Where(m => m.Status == OutboxMessageStatus.Sent)
                .Where(m => m.SentAt != null && m.SentAt < olderThan)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Outbox cleanup completed. Deleted {Count} sent messages older than {Date}",
                    deletedCount, olderThan);
            }
            else
            {
                _logger.LogDebug("Outbox cleanup completed. No messages to delete");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup outbox");
        }
    }
}
